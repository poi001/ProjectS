using MoreMountains.Feedbacks;
using UnityEngine;

// 플레이어나 보스가 죽을 때의 KO 연출을 담당한다. BattleManagers에 포함되어 전투 씬에만 존재한다.
//  - 죽은 순간(CharacterDiedEvent): KO 사운드 재생 + 슬로우 모션 시작 + 죽은 캐릭터로 카메라 줌인
//  - 사망 애니메이션이 끝났을 때(CharacterDeathFinishedEvent): 슬로우 모션 해제 + 카메라를 원래 위치·크기로 복귀
// 연출 대상은 플레이어와 보스(Enemy.IsBoss)뿐이다. 보스가 소환한 잡몹의 죽음에는 반응하지 않는다.
// 슬로우 모션, 줌, 카메라 이동은 Feel(MMF_Player)로 구성해 두고, 이 클래스는 값(줌 목적지 등)을 정해 재생만 호출한다.
// 카메라는 고정이고 화면에 맵 전체가 담겨 있으므로(GDD 6.1), 줌할 때는 카메라 중심을 죽은 캐릭터 쪽으로 옮기되
// 원래 화면 밖이 보이지 않도록 제한한다.
// KO 사운드는 SoundManager로 재생한다. (피치·볼륨은 SoundDataSO 설정을 따른다)
public class KOEffectManager : MonoBehaviour
{
    [Header("Feel 피드백")]
    [Tooltip("KO 순간에 재생하는 MMF_Player입니다. 슬로우 모션 시작(Timescale Modifier), 줌인(Camera Orthographic Size), 카메라 이동(Position, 목적지는 Focus Point)이 들어 있어야 합니다.")]
    [SerializeField] private MMF_Player _enterFeedbacks;

    [Tooltip("사망 애니메이션이 끝났을 때 재생하는 MMF_Player입니다. 슬로우 모션 해제(Timescale Modifier Reset), 줌아웃(Camera Orthographic Size), 카메라 복귀(Position, 목적지는 Home Point)가 들어 있어야 합니다.")]
    [SerializeField] private MMF_Player _exitFeedbacks;

    [Tooltip("줌인할 때 카메라가 이동할 목적지입니다. KO 순간마다 죽은 캐릭터 위치로 옮겨지며, Enter Feedbacks의 Position 피드백이 목적지로 사용합니다.")]
    [SerializeField] private Transform _focusPoint;

    [Tooltip("줌아웃할 때 카메라가 돌아갈 목적지입니다. 시작할 때 카메라의 원래 위치로 정해지며, Exit Feedbacks의 Position 피드백이 목적지로 사용합니다.")]
    [SerializeField] private Transform _homePoint;

    [Header("카메라")]
    [Tooltip("연출할 메인 카메라입니다. 직교(Orthographic) 카메라여야 합니다. 씬마다 다르므로 씬에 놓인 BattleManagers 인스턴스에서 연결합니다.")]
    [SerializeField] private Camera _camera;

    [Tooltip("줌인했을 때의 카메라 직교 크기(화면 세로 절반의 월드 길이)입니다. 원래 크기보다 작을수록 더 확대됩니다. 원래 크기 이상이면 줌하지 않습니다.")]
    [SerializeField, Min(0.1f)] private float _zoomedOrthographicSize = 3f;

    [Header("사운드")]
    [Tooltip("플레이어가 죽을 때 재생하는 사운드 데이터입니다. 비워 두면 소리가 나지 않습니다.")]
    [SerializeField] private SoundDataSO _playerKoSound;

    [Tooltip("보스가 죽을 때 재생하는 사운드 데이터입니다. 비워 두면 소리가 나지 않습니다.")]
    [SerializeField] private SoundDataSO _bossKoSound;

    private MMF_CameraOrthographicSize _enterZoom;
    private MMF_CameraOrthographicSize _exitZoom;
    private Vector3 _homePosition;
    private float _homeOrthographicSize;
    // 지금 KO 연출 중인 캐릭터. 연출 중이 아니면 null이다.
    private GameObject _activeTarget;

    private void Awake()
    {
        if (!ValidateSettings())
        {
            // Awake에서 꺼 두면 OnEnable이 호출되지 않아 이벤트를 구독하지 않는다.
            enabled = false;
            return;
        }

        _homePosition = _camera.transform.position;
        _homeOrthographicSize = _camera.orthographicSize;
        _homePoint.position = _homePosition;

        _enterZoom = _enterFeedbacks.GetFeedbackOfType<MMF_CameraOrthographicSize>();
        _exitZoom = _exitFeedbacks.GetFeedbackOfType<MMF_CameraOrthographicSize>();

        // 카메라는 씬 오브젝트라 프리팹에 미리 연결할 수 없으므로, 이동 피드백의 대상을 여기서 지정한다.
        SetMoveTarget(_enterFeedbacks);
        SetMoveTarget(_exitFeedbacks);

        // 대상을 지정한 뒤에 초기화해야 하므로 두 플레이어의 Initialization Mode는 Script여야 한다.
        _enterFeedbacks.Initialization();
        _exitFeedbacks.Initialization();
    }

    private void OnEnable()
    {
        EventBus<CharacterDiedEvent>.Subscribe(OnCharacterDied);
        EventBus<CharacterDeathFinishedEvent>.Subscribe(OnCharacterDeathFinished);
    }

    private void OnDisable()
    {
        EventBus<CharacterDiedEvent>.Unsubscribe(OnCharacterDied);
        EventBus<CharacterDeathFinishedEvent>.Unsubscribe(OnCharacterDeathFinished);

        // 연출 도중에 매니저가 사라져도(씬 전환 등) 슬로우 모션과 카메라가 남지 않게 되돌린다.
        if (_activeTarget != null)
        {
            RestoreImmediately();
        }
    }

    private void OnCharacterDied(CharacterDiedEvent diedEvent)
    {
        bool isPlayer = diedEvent.Target.TryGetComponent(out Player _);

        if (!isPlayer && !IsBoss(diedEvent.Target))
        {
            return;
        }

        PlayKoSound(isPlayer ? _playerKoSound : _bossKoSound);

        // 이미 다른 캐릭터의 KO 연출 중이면 소리만 낸다.
        if (_activeTarget != null)
        {
            return;
        }

        _activeTarget = diedEvent.Target;
        PlayEnter(diedEvent.Target);
    }

    private void OnCharacterDeathFinished(CharacterDeathFinishedEvent finishedEvent)
    {
        if (_activeTarget == null || finishedEvent.Target != _activeTarget)
        {
            return;
        }

        _activeTarget = null;
        PlayExit();
    }

    private void PlayEnter(GameObject target)
    {
        _exitFeedbacks.StopFeedbacks();
        _focusPoint.position = GetFocusPosition(target);

        // 지금 크기에서 줌 크기로. (복귀 중에 또 죽은 경우를 위해 현재 크기에서 시작한다)
        _enterZoom.RemapOrthographicSizeZero = _camera.orthographicSize;
        _enterZoom.RemapOrthographicSizeOne = Mathf.Min(_zoomedOrthographicSize, _homeOrthographicSize);

        _enterFeedbacks.PlayFeedbacks();
    }

    private void PlayExit()
    {
        _enterFeedbacks.StopFeedbacks();

        _exitZoom.RemapOrthographicSizeZero = _camera.orthographicSize;
        _exitZoom.RemapOrthographicSizeOne = _homeOrthographicSize;

        _exitFeedbacks.PlayFeedbacks();
    }

    // 카메라 중심이 죽은 캐릭터를 향하되, 줌한 화면이 원래 화면 밖으로 나가지 않도록 이동 범위를 제한한다.
    private Vector3 GetFocusPosition(GameObject target)
    {
        Vector2 center = target.transform.position;

        if (target.TryGetComponent(out Character character) && character.Hurtbox != null)
        {
            center = character.Hurtbox.bounds.center;
        }

        float zoomedSize = Mathf.Min(_zoomedOrthographicSize, _homeOrthographicSize);
        float maxOffsetX = (_homeOrthographicSize - zoomedSize) * _camera.aspect;
        float maxOffsetY = _homeOrthographicSize - zoomedSize;

        return new Vector3(
            Mathf.Clamp(center.x, _homePosition.x - maxOffsetX, _homePosition.x + maxOffsetX),
            Mathf.Clamp(center.y, _homePosition.y - maxOffsetY, _homePosition.y + maxOffsetY),
            _homePosition.z);
    }

    // 피드백을 거치지 않고 즉시 원래 상태로 되돌린다.
    private void RestoreImmediately()
    {
        _activeTarget = null;
        _enterFeedbacks.StopFeedbacks();
        _exitFeedbacks.StopFeedbacks();
        Time.timeScale = 1f;

        if (_camera != null)
        {
            _camera.transform.position = _homePosition;
            _camera.orthographicSize = _homeOrthographicSize;
        }
    }

    private void SetMoveTarget(MMF_Player feedbackPlayer)
    {
        MMF_Position move = feedbackPlayer.GetFeedbackOfType<MMF_Position>();

        if (move != null)
        {
            move.AnimatePositionTarget = _camera.gameObject;
        }
    }

    private static bool IsBoss(GameObject target)
    {
        return target.TryGetComponent(out Enemy enemy) && enemy.IsBoss;
    }

    private static void PlayKoSound(SoundDataSO soundData)
    {
        if (soundData == null || !SoundManager.HasInstance)
        {
            return;
        }

        SoundManager.Instance.PlaySfx(soundData);
    }

    private bool ValidateSettings()
    {
        bool isValid = true;

        if (_enterFeedbacks == null || _exitFeedbacks == null || _focusPoint == null || _homePoint == null)
        {
            Debug.LogError("[KOEffectManager] Feel 피드백 또는 이동 목적지(Focus/Home Point)가 지정되지 않았습니다.", this);
            isValid = false;
        }

        if (_camera == null)
        {
            Debug.LogError("[KOEffectManager] 연출할 카메라가 지정되지 않았습니다. 씬의 BattleManagers 인스턴스에서 연결해야 합니다.", this);
            isValid = false;
        }
        else if (!_camera.orthographic)
        {
            Debug.LogError("[KOEffectManager] 카메라가 직교(Orthographic)가 아니라 줌 연출을 할 수 없습니다.", this);
            isValid = false;
        }

        if (isValid && (_enterFeedbacks.GetFeedbackOfType<MMF_CameraOrthographicSize>() == null || _exitFeedbacks.GetFeedbackOfType<MMF_CameraOrthographicSize>() == null))
        {
            Debug.LogError("[KOEffectManager] Enter/Exit Feedbacks에 Camera Orthographic Size 피드백이 없습니다.", this);
            isValid = false;
        }

        return isValid;
    }
}

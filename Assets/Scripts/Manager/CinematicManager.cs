using MoreMountains.Feedbacks;
using UnityEngine;

// 시네마틱(연출 장면)의 카메라를 담당한다. BattleManagers에 포함되어 전투 씬에만 존재한다.
//  - 시작(CinematicStartedEvent): 비출 대상 쪽으로 카메라를 옮기며 줌인
//  - 끝(CinematicEndedEvent): 카메라를 원래 위치·크기로 복귀
// 시네마틱은 여는 쪽(보스의 연출 상태 등)이 두 이벤트를 발행해 시작하고 끝낸다. 이 클래스는 카메라만 맡고,
// 위아래 검은 띠와 전투 UI 숨김은 CinematicUI(BattleUI), 플레이어 입력 잠금은 Player가 같은 이벤트를 받아 처리한다.
// 줌과 카메라 이동은 Feel(MMF_Player)로 구성해 두고, 이 클래스는 값(줌 목적지 등)을 정해 재생만 호출한다. (KOEffectManager와 같은 방식, 슬로우 모션 없음)
// 카메라는 씬마다 연결하지 않고 시작할 때 메인 카메라(MainCamera 태그)를 찾아 쓴다. 카메라의 부모(CameraRig)가 있으면 부모를 이동한다.
// 카메라는 고정이고 화면에 맵 전체가 담겨 있으므로, 줌할 때는 카메라 중심을 대상 쪽으로 옮기되 원래 화면 밖이 보이지 않도록 제한한다.
public class CinematicManager : MonoBehaviour
{
    [Header("Feel 피드백")]
    [Tooltip("시네마틱이 시작될 때 재생하는 MMF_Player입니다. 줌인(Camera Orthographic Size)과 카메라 이동(Position, 목적지는 Focus Point)이 들어 있어야 합니다. 초기화 방식은 Script로 둡니다.\n줌·이동에 걸리는 시간은 이 피드백에서 조절합니다.")]
    [SerializeField] private MMF_Player _enterFeedbacks;

    [Tooltip("시네마틱이 끝날 때 재생하는 MMF_Player입니다. 줌아웃(Camera Orthographic Size)과 카메라 복귀(Position, 목적지는 Home Point)가 들어 있어야 합니다. 초기화 방식은 Script로 둡니다.")]
    [SerializeField] private MMF_Player _exitFeedbacks;

    [Tooltip("줌인할 때 카메라가 이동할 목적지입니다. 시네마틱이 시작될 때마다 비출 대상의 위치로 옮겨지며, Enter Feedbacks의 Position 피드백이 목적지로 사용합니다.")]
    [SerializeField] private Transform _focusPoint;

    [Tooltip("줌아웃할 때 카메라가 돌아갈 목적지입니다. 시작할 때 카메라의 원래 위치로 정해지며, Exit Feedbacks의 Position 피드백이 목적지로 사용합니다.")]
    [SerializeField] private Transform _homePoint;

    [Header("카메라")]
    [Tooltip("줌인했을 때의 카메라 직교 크기(화면 세로 절반의 월드 길이)입니다. 원래 크기보다 작을수록 더 확대됩니다. 원래 크기 이상이면 줌하지 않습니다.\n원래 크기 5 기준으로 KO 연출(3)의 절반만 줌인하는 값이 4입니다.")]
    [SerializeField, Min(0.1f)] private float _zoomedOrthographicSize = 4f;

    private Camera _camera;
    // 이동시킬 대상. 카메라의 부모(CameraRig)가 있으면 부모, 없으면 카메라 자체다. (피격 흔들림은 카메라의 로컬 위치만 바꾼다)
    private Transform _cameraMotionTarget;
    private MMF_CameraOrthographicSize _enterZoom;
    private MMF_CameraOrthographicSize _exitZoom;
    private Vector3 _homePosition;
    private float _homeOrthographicSize;
    private bool _isPlaying;

    private void Awake()
    {
        if (!ValidateSettings())
        {
            // Awake에서 꺼 두면 OnEnable이 호출되지 않아 이벤트를 구독하지 않는다.
            enabled = false;
            return;
        }

        _cameraMotionTarget = _camera.transform.parent != null ? _camera.transform.parent : _camera.transform;
        _homePosition = _cameraMotionTarget.position;
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
        EventBus<CinematicStartedEvent>.Subscribe(OnCinematicStarted);
        EventBus<CinematicEndedEvent>.Subscribe(OnCinematicEnded);
    }

    private void OnDisable()
    {
        EventBus<CinematicStartedEvent>.Unsubscribe(OnCinematicStarted);
        EventBus<CinematicEndedEvent>.Unsubscribe(OnCinematicEnded);

        // 시네마틱 도중에 매니저가 사라져도(씬 전환 등) 카메라가 줌인된 채로 남지 않게 되돌린다.
        if (_isPlaying)
        {
            RestoreImmediately();
        }
    }

    private void OnCinematicStarted(CinematicStartedEvent cinematicEvent)
    {
        // 이미 시네마틱 중이거나 비출 대상이 없으면 카메라를 움직이지 않는다. 씬이 닫히는 중이라 카메라가 먼저 파괴된 경우에도 건드리지 않는다.
        if (_isPlaying || cinematicEvent.FocusTarget == null || _camera == null)
        {
            return;
        }

        _isPlaying = true;
        float currentSize = StopAndKeepSize(_exitFeedbacks);
        float zoomedSize = _zoomedOrthographicSize;

        if (cinematicEvent.HasCustomFraming)
        {
            // 여는 쪽이 정한 구도를 그대로 쓴다. 원래 화면 밖이 보이지 않게 하는 제한은 적용하지 않는다. (검은 띠가 가리는 범위까지 여는 쪽이 계산한다)
            Vector2 center = cinematicEvent.FramingCenter;
            _focusPoint.position = new Vector3(center.x, center.y, _homePosition.z);
            zoomedSize = cinematicEvent.FramingOrthographicSize;
        }
        else
        {
            _focusPoint.position = GetFocusPosition(cinematicEvent.FocusTarget);
        }

        // 지금 크기에서 줌 크기로. (복귀 중에 다시 시작한 경우를 위해 현재 크기에서 시작한다)
        _enterZoom.RemapOrthographicSizeZero = currentSize;
        _enterZoom.RemapOrthographicSizeOne = Mathf.Min(zoomedSize, _homeOrthographicSize);

        _enterFeedbacks.PlayFeedbacks();
    }

    private void OnCinematicEnded(CinematicEndedEvent cinematicEvent)
    {
        if (!_isPlaying)
        {
            return;
        }

        _isPlaying = false;

        // 씬이 닫히는 중에 보스가 Release되며 시네마틱을 닫는 경우, 카메라가 먼저 파괴되어 있을 수 있다. 되돌릴 대상이 없으므로 끝낸다.
        if (_camera == null)
        {
            return;
        }

        // KO 연출이 카메라를 이어받는 경우에는 건드리지 않는다. 여기서 되돌리면 KO의 줌인과 서로 다른 쪽으로 카메라를 움직이게 된다.
        // 카메라는 KO 연출이 끝나며 원래 위치·크기로 돌아간다. (KOEffectManager)
        // 줌인이 아직 진행 중이었다면(시작 직후에 죽은 경우) 그 줌인만 멈춰, KO의 줌인과 동시에 카메라를 움직이지 않게 한다. 크기와 위치는 멈춘 자리 그대로 둔다.
        if (cinematicEvent.KeepsCamera)
        {
            StopAndKeepSize(_enterFeedbacks);
            return;
        }

        float currentSize = StopAndKeepSize(_enterFeedbacks);

        _exitZoom.RemapOrthographicSizeZero = currentSize;
        _exitZoom.RemapOrthographicSizeOne = _homeOrthographicSize;

        _exitFeedbacks.PlayFeedbacks();
    }

    // 피드백을 멈추고, 멈추기 직전의 카메라 크기를 반환한다.
    // Feel의 줌 셰이커는 멈출 때(이미 끝난 줌이어도) 카메라 크기를 자신의 기본값으로 되돌리므로, 멈춘 뒤 원래 크기로 다시 맞춘다.
    // 이렇게 하지 않으면 다음 줌이 기본값에서 시작해 화면이 한 번에 튄다.
    private float StopAndKeepSize(MMF_Player feedbackPlayer)
    {
        float currentSize = _camera.orthographicSize;
        feedbackPlayer.StopFeedbacks();
        _camera.orthographicSize = currentSize;
        return currentSize;
    }

    // 카메라 중심이 대상을 향하되, 줌한 화면이 원래 화면 밖으로 나가지 않도록 이동 범위를 제한한다.
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
        _isPlaying = false;
        _enterFeedbacks.StopFeedbacks();
        _exitFeedbacks.StopFeedbacks();

        if (_camera != null && _cameraMotionTarget != null)
        {
            _cameraMotionTarget.position = _homePosition;
            _camera.orthographicSize = _homeOrthographicSize;
        }
    }

    private void SetMoveTarget(MMF_Player feedbackPlayer)
    {
        MMF_Position move = feedbackPlayer.GetFeedbackOfType<MMF_Position>();

        if (move != null)
        {
            move.AnimatePositionTarget = _cameraMotionTarget.gameObject;
        }
    }

    private bool ValidateSettings()
    {
        if (_enterFeedbacks == null || _exitFeedbacks == null || _focusPoint == null || _homePoint == null)
        {
            Debug.LogError("CinematicManager: Enter/Exit Feedbacks, Focus Point, Home Point가 모두 연결되어 있어야 합니다. 시네마틱 카메라를 끕니다.", this);
            return false;
        }

        _camera = Camera.main;

        if (_camera == null)
        {
            Debug.LogError("CinematicManager: 메인 카메라(MainCamera 태그)를 찾지 못했습니다. 시네마틱 카메라를 끕니다.", this);
            return false;
        }

        if (!_camera.orthographic)
        {
            Debug.LogError("CinematicManager: 메인 카메라가 직교(Orthographic) 카메라가 아닙니다. 시네마틱 카메라를 끕니다.", this);
            return false;
        }

        if (_camera.GetComponent<MMCameraOrthographicSizeShaker>() == null)
        {
            Debug.LogError("CinematicManager: 메인 카메라에 MMCameraOrthographicSizeShaker가 없어 줌할 수 없습니다. 시네마틱 카메라를 끕니다.", _camera);
            return false;
        }

        return true;
    }
}

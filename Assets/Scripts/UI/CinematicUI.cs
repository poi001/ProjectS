using DG.Tweening;
using UnityEngine;

// 시네마틱(연출 장면) 동안의 화면 UI를 담당한다. BattleUI 프리팹의 루트에 붙인다.
//  - 시작(CinematicStartedEvent): 위아래에 영화처럼 검은 띠가 들어오고, 전투 UI(보스·플레이어 HUD)가 서서히 사라진다.
//  - 끝(CinematicEndedEvent): 검은 띠가 나가고 전투 UI가 다시 나타난다.
// 카메라 줌은 CinematicManager, 플레이어 입력 잠금은 Player가 같은 이벤트를 받아 처리한다.
// 값 보간은 DOTween으로 하며, 오브젝트가 사라질 때 트윈이 남지 않도록 SetLink로 묶는다.
public class CinematicUI : MonoBehaviour
{
    [Header("검은 띠")]
    [Tooltip("화면 위쪽 검은 띠입니다. 위쪽 가장자리에 붙어 가로로 꽉 차도록 앵커를 잡은 Image의 RectTransform을 연결합니다. 시작할 때 높이 0으로 숨겨 둡니다.")]
    [SerializeField] private RectTransform _topBar;
    [Tooltip("화면 아래쪽 검은 띠입니다. 아래쪽 가장자리에 붙어 가로로 꽉 차도록 앵커를 잡은 Image의 RectTransform을 연결합니다.")]
    [SerializeField] private RectTransform _bottomBar;
    [Tooltip("검은 띠 하나의 높이입니다. 캔버스 기준 해상도(1920x1080) 단위입니다. (예: 110이면 화면 높이의 약 10%)")]
    [SerializeField, Min(0f)] private float _barHeight = 110f;

    [Header("전투 UI")]
    [Tooltip("시네마틱 동안 숨길 전투 UI 묶음들입니다. 각 묶음(BossHUD, PlayerHUD)에 CanvasGroup을 붙여 연결합니다. 숨기는 동안 투명해집니다.")]
    [SerializeField] private CanvasGroup[] _hudGroups = new CanvasGroup[0];

    [Header("연출 시간")]
    [Tooltip("검은 띠가 들어오고 나가는 시간, 전투 UI가 사라지고 나타나는 시간입니다. 카메라 줌 시간(CinematicManager의 피드백, 0.4초)과 맞춰 둡니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _duration = 0.4f;

    private bool _isShown;

    private void Awake()
    {
        ApplyImmediately(false);
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

        // 시네마틱 도중에 꺼지면 다음에 켜졌을 때 띠가 남지 않도록 바로 되돌린다.
        ApplyImmediately(false);
    }

    private void OnCinematicStarted(CinematicStartedEvent cinematicEvent)
    {
        Show(true);
    }

    private void OnCinematicEnded(CinematicEndedEvent cinematicEvent)
    {
        Show(false);
    }

    // 검은 띠를 들이고 전투 UI를 숨기거나(true), 띠를 내보내고 전투 UI를 되돌린다(false). 진행 중이던 연출은 지금 값에서 이어서 바꾼다.
    private void Show(bool isShown)
    {
        if (_isShown == isShown)
        {
            return;
        }

        _isShown = isShown;
        KillTweens();

        float barHeight = isShown ? _barHeight : 0f;
        float hudAlpha = isShown ? 0f : 1f;

        if (_topBar != null)
        {
            _topBar.DOSizeDelta(new Vector2(_topBar.sizeDelta.x, barHeight), _duration).SetEase(Ease.OutCubic).SetLink(gameObject);
        }

        if (_bottomBar != null)
        {
            _bottomBar.DOSizeDelta(new Vector2(_bottomBar.sizeDelta.x, barHeight), _duration).SetEase(Ease.OutCubic).SetLink(gameObject);
        }

        for (int i = 0; i < _hudGroups.Length; i++)
        {
            if (_hudGroups[i] != null)
            {
                _hudGroups[i].DOFade(hudAlpha, _duration).SetLink(gameObject);
            }
        }
    }

    // 연출 없이 즉시 적용한다. (초기화, 비활성화 시)
    private void ApplyImmediately(bool isShown)
    {
        _isShown = isShown;
        KillTweens();

        float barHeight = isShown ? _barHeight : 0f;

        if (_topBar != null)
        {
            _topBar.sizeDelta = new Vector2(_topBar.sizeDelta.x, barHeight);
        }

        if (_bottomBar != null)
        {
            _bottomBar.sizeDelta = new Vector2(_bottomBar.sizeDelta.x, barHeight);
        }

        for (int i = 0; i < _hudGroups.Length; i++)
        {
            if (_hudGroups[i] != null)
            {
                _hudGroups[i].alpha = isShown ? 0f : 1f;
            }
        }
    }

    private void KillTweens()
    {
        if (_topBar != null)
        {
            _topBar.DOKill();
        }

        if (_bottomBar != null)
        {
            _bottomBar.DOKill();
        }

        for (int i = 0; i < _hudGroups.Length; i++)
        {
            if (_hudGroups[i] != null)
            {
                _hudGroups[i].DOKill();
            }
        }
    }
}

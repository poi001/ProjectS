using UnityEngine;
using UnityEngine.UI;

// 플레이어 기력바 UI. 플레이어 체력바 바로 아래에 둔다. (BattleUI의 PlayerHUD/PlayerVitalBars)
// 연결된 PlayerStamina의 Changed를 구독해 채움 비율을 즉시 바꾼다. 체력바와 달리 지연(잔상) 바나 보간 연출이 없다.
// 전투 준비가 끝나면(BattleReadyEvent) 소환된 플레이어의 기력에 스스로 연결하므로 씬마다 따로 연결하지 않는다. 실행 중 Bind로 바꿀 수도 있다.
// 채움 비율 표현은 HPBar와 같다. Image가 스프라이트가 있는 Filled 타입이면 fillAmount를,
// 그 외에는 RectTransform의 anchorMax.x로 표현한다. (anchorMin.x = 0, 왼쪽에서 오른쪽으로 채움)
public class StaminaBar : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("현재 기력 비율만큼 즉시 채워지는 바 이미지입니다. 색은 이 이미지의 색으로 정합니다.")]
    [SerializeField] private Image _fillImage;

    private PlayerStamina _target;

    private void Awake()
    {
        // 연결 전에는 가득 찬 모습으로 둔다.
        ApplyRatio(1f);
    }

    private void OnEnable()
    {
        EventBus<BattleReadyEvent>.Subscribe(OnBattleReady);
        SubscribeTarget();
    }

    private void OnDisable()
    {
        EventBus<BattleReadyEvent>.Unsubscribe(OnBattleReady);
        UnsubscribeTarget();
    }

    // 표시할 기력을 바꾼다. 바꾸는 즉시 새 대상의 기력으로 맞춘다. null이면 연결만 끊는다.
    public void Bind(PlayerStamina target)
    {
        if (_target == target)
        {
            return;
        }

        if (isActiveAndEnabled)
        {
            UnsubscribeTarget();
        }

        _target = target;

        if (isActiveAndEnabled)
        {
            SubscribeTarget();
        }
    }

    private void OnBattleReady(BattleReadyEvent readyEvent)
    {
        if (readyEvent.Player != null)
        {
            Bind(readyEvent.Player.Stamina);
        }
    }

    private void SubscribeTarget()
    {
        if (_target == null)
        {
            return;
        }

        _target.Changed += OnTargetChanged;
        ApplyRatio(_target.Ratio);
    }

    private void UnsubscribeTarget()
    {
        if (_target == null)
        {
            return;
        }

        _target.Changed -= OnTargetChanged;
    }

    private void OnTargetChanged()
    {
        ApplyRatio(_target.Ratio);
    }

    private void ApplyRatio(float ratio)
    {
        if (_fillImage == null)
        {
            return;
        }

        ratio = Mathf.Clamp01(ratio);

        if (_fillImage.type == Image.Type.Filled && _fillImage.sprite != null)
        {
            _fillImage.fillAmount = ratio;
            return;
        }

        RectTransform rectTransform = _fillImage.rectTransform;
        Vector2 anchorMax = rectTransform.anchorMax;
        anchorMax.x = ratio;
        rectTransform.anchorMax = anchorMax;
    }
}

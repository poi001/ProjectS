using UnityEngine;
using UnityEngine.UI;

// 보스 패턴 게이지 UI. 보스 체력바 아래에 둔다. 연결된 PatternGauge의 Changed를 구독해 채움 비율을 바로 갱신한다.
// 대상은 인스펙터에서 지정하거나, 실행 중 Bind로 바꾼다. 게이지를 쓰지 않는 보스(PatternGauge가 null)를 Bind하면 숨겨진다.
// 채움 비율 표현은 HPBar와 같다. Image가 스프라이트가 있는 Filled 타입이면 fillAmount를,
// 그 외에는 RectTransform의 anchorMax.x로 표현한다. (anchorMin.x = 0, 왼쪽에서 오른쪽으로 채움)
public class PatternGaugeBar : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("표시할 보스의 PatternGauge입니다. 비워 두면 빈 게이지로 보이며, 실행 중에는 Bind로 바꿀 수 있습니다.")]
    [SerializeField] private PatternGauge _target;

    [Header("참조")]
    [Tooltip("게이지 비율만큼 채워지는 바 이미지입니다.")]
    [SerializeField] private Image _fillImage;

    private void Awake()
    {
        ApplyRatio(0f);
    }

    private void OnEnable()
    {
        SubscribeTarget();
    }

    private void OnDisable()
    {
        UnsubscribeTarget();
    }

    // 표시할 게이지를 바꾼다. null이면 연결을 끊고 게이지 UI를 숨긴다.
    public void Bind(PatternGauge target)
    {
        if (isActiveAndEnabled)
        {
            UnsubscribeTarget();
        }

        _target = target;

        if (_target == null)
        {
            gameObject.SetActive(false);
            return;
        }

        // 숨겨져 있었다면 다시 켤 때 OnEnable에서 구독한다.
        if (isActiveAndEnabled)
        {
            SubscribeTarget();
        }
        else
        {
            gameObject.SetActive(true);
        }
    }

    // 구독과 동시에 현재 값으로 맞춘다.
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

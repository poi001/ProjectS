using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 보스 패턴 게이지 UI. 보스 체력바 아래에 둔다. 연결된 PatternGauge의 Changed를 구독해 채움 비율을 바로 갱신한다.
// 대상은 인스펙터에서 지정하거나, 실행 중 Bind로 바꾼다. 게이지를 쓰지 않는 보스(PatternGauge가 null)를 Bind하면 숨겨진다.
// 채움 비율 표현은 HPBar와 같다. Image가 스프라이트가 있는 Filled 타입이면 fillAmount를,
// 그 외에는 RectTransform의 anchorMax.x로 표현한다. (anchorMin.x = 0, 왼쪽에서 오른쪽으로 채움)
// 게이지가 꽉 차면 바가 한 번 튕기고, 꽉 찬 동안 채움 색이 원래 색과 강조 색 사이를 오가며 깜빡인다. (DOTween, 게임 시간 기준)
// 꽉 찬 상태가 끝나면(게이지 감소, 대상 교체, 비활성화) 원래 색과 크기로 돌아온다.
public class PatternGaugeBar : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("표시할 보스의 PatternGauge입니다. 비워 두면 빈 게이지로 보이며, 실행 중에는 Bind로 바꿀 수 있습니다.")]
    [SerializeField] private PatternGauge _target;

    [Header("참조")]
    [Tooltip("게이지 비율만큼 채워지는 바 이미지입니다.")]
    [SerializeField] private Image _fillImage;

    [Header("꽉 찼을 때 강조")]
    [Tooltip("게이지가 꽉 차는 순간 바가 튕기며 커지는 정도입니다. 원래 크기(1)에 더해지는 배율이며 X는 가로, Y는 세로입니다. (0, 0)이면 튕기지 않습니다.")]
    [SerializeField] private Vector2 _fullPunchScale = new Vector2(0.02f, 0.6f);

    [Tooltip("튕김이 끝나 원래 크기로 돌아올 때까지 걸리는 시간입니다. (단위: 초, 게임 시간)")]
    [SerializeField, Min(0f)] private float _fullPunchDuration = 0.3f;

    [Tooltip("튕기는 동안 흔들리는 횟수입니다. 클수록 잘게 여러 번 떨립니다.")]
    [SerializeField, Min(1)] private int _fullPunchVibrato = 6;

    [Tooltip("꽉 찬 동안 채움 색이 원래 색과 오가는 강조 색입니다. 보스의 기절 가능 표시(파란 외곽선)와 같은 계열로 둡니다.")]
    [SerializeField] private Color _fullBlinkColor = new Color(0.35f, 0.75f, 1f, 1f);

    [Tooltip("채움 색이 원래 색에서 강조 색까지 한 번 바뀌는 데 걸리는 시간입니다. 왕복 한 번은 이 값의 두 배입니다. (단위: 초, 게임 시간)")]
    [SerializeField, Min(0.01f)] private float _fullBlinkHalfDuration = 0.35f;

    private Color _fillNormalColor;
    private Vector3 _initialScale;
    private Tween _punchTween;
    private Tween _blinkTween;
    private bool _isFullEffectActive;

    private void Awake()
    {
        _initialScale = transform.localScale;

        if (_fillImage != null)
        {
            _fillNormalColor = _fillImage.color;
        }

        ApplyRatio(0f);
    }

    private void OnEnable()
    {
        SubscribeTarget();
    }

    private void OnDisable()
    {
        UnsubscribeTarget();
        StopFullEffect();
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

    // 구독과 동시에 현재 값으로 맞춘다. 이미 꽉 찬 게이지에 연결된 경우에는 튕김 없이 깜빡임만 켠다.
    private void SubscribeTarget()
    {
        if (_target == null)
        {
            StopFullEffect();
            return;
        }

        _target.Changed += OnTargetChanged;
        ApplyRatio(_target.Ratio);
        RefreshFullEffect(false);
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
        RefreshFullEffect(true);
    }

    // 게이지가 꽉 찼는지에 맞춰 강조 연출을 켜거나 끈다. 상태가 그대로면 아무것도 하지 않는다.
    private void RefreshFullEffect(bool shouldPlayPunch)
    {
        if (!_target.IsFull)
        {
            StopFullEffect();
            return;
        }

        if (_isFullEffectActive)
        {
            return;
        }

        _isFullEffectActive = true;

        if (shouldPlayPunch && _fullPunchDuration > 0f && _fullPunchScale != Vector2.zero)
        {
            _punchTween = transform.DOPunchScale(new Vector3(_fullPunchScale.x, _fullPunchScale.y, 0f), _fullPunchDuration, _fullPunchVibrato)
                .SetLink(gameObject);
        }

        if (_fillImage != null)
        {
            _blinkTween = _fillImage.DOColor(_fullBlinkColor, _fullBlinkHalfDuration)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo)
                .SetLink(gameObject);
        }
    }

    // 강조 연출을 멈추고 원래 색과 크기로 되돌린다.
    private void StopFullEffect()
    {
        if (!_isFullEffectActive)
        {
            return;
        }

        _isFullEffectActive = false;
        _punchTween?.Kill();
        _blinkTween?.Kill();
        _punchTween = null;
        _blinkTween = null;
        transform.localScale = _initialScale;

        if (_fillImage != null)
        {
            _fillImage.color = _fillNormalColor;
        }
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

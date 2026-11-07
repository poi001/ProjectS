using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

// 체력바 UI. 연결된 캐릭터(CharacterVital)의 HealthChanged를 구독해 자동으로 갱신한다.
// 대상은 인스펙터에서 지정하거나, 실행 중 Bind로 바꾼다. (보스가 런타임에 생성되는 경우 등)
// 대상 없이 SetHealth / SetHealthImmediate를 직접 호출해 써도 된다.
// 채움 바는 즉시 바뀌고, 지연 바는 잠시 기다렸다가 천천히 따라 줄어들어 방금 받은 피해량을 보여 준다.
// 스프라이트 없이도 동작하도록, Image가 스프라이트가 있는 Filled 타입이면 fillAmount를,
// 그 외에는 RectTransform의 anchorMax.x로 채움 비율을 표현한다. (anchorMin.x = 0, 왼쪽에서 오른쪽으로 채움)
public class HPBar : MonoBehaviour
{
    [Header("대상")]
    [Tooltip("체력을 표시할 캐릭터의 CharacterVital입니다. 비워 두면 자동 갱신하지 않으며, 실행 중에는 Bind로 바꿀 수 있습니다.")]
    [SerializeField] private CharacterVital _target;

    [Header("참조")]
    [Tooltip("현재 체력 비율만큼 즉시 채워지는 바 이미지입니다.")]
    [SerializeField] private Image _fillImage;

    [Tooltip("피해를 받으면 잠시 뒤 천천히 줄어드는 지연(잔상) 바 이미지입니다. 채움 바보다 뒤(위쪽 형제)에 둡니다.")]
    [SerializeField] private Image _delayImage;

    [Header("지연 바 연출")]
    [Tooltip("피해를 받은 뒤 지연 바가 줄어들기 시작할 때까지 기다리는 시간입니다. 기다리는 중에 또 맞으면 처음부터 다시 기다립니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _drainDelay = 0.4f;

    [Tooltip("지연 바가 현재 체력 위치까지 줄어드는 데 걸리는 시간입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _drainDuration = 0.5f;

    [Tooltip("지연 바가 줄어들 때 사용하는 보간 곡선입니다.")]
    [SerializeField] private Ease _drainEase = Ease.OutQuad;

    private float _fillRatio = 1f;
    private float _delayRatio = 1f;
    private Tween _drainTween;

    public float FillRatio => _fillRatio;

    private void Awake()
    {
        ApplyRatio(_fillImage, _fillRatio);
        ApplyRatio(_delayImage, _delayRatio);
    }

    private void OnEnable()
    {
        SubscribeTarget();
    }

    private void OnDisable()
    {
        UnsubscribeTarget();

        // 비활성화 중에는 연출을 이어가지 않고 지연 바를 현재 체력에 맞춘다.
        _drainTween?.Kill();
        SetDelayRatio(_fillRatio);
    }

    // 표시할 캐릭터를 바꾼다. 바꾸는 즉시 연출 없이 새 대상의 체력으로 맞춘다. null이면 연결만 끊는다.
    public void Bind(CharacterVital target)
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

    // 체력을 갱신한다. 줄어들면 지연 바 연출을 재생하고, 늘어나면 두 바를 바로 맞춘다.
    public void SetHealth(float currentHealth, float maxHealth)
    {
        float ratio = CalculateRatio(currentHealth, maxHealth);
        _fillRatio = ratio;
        ApplyRatio(_fillImage, _fillRatio);

        if (_delayRatio <= ratio || !isActiveAndEnabled)
        {
            _drainTween?.Kill();
            SetDelayRatio(ratio);
            return;
        }

        _drainTween?.Kill();
        _drainTween = DOTween.To(GetDelayRatio, SetDelayRatio, ratio, _drainDuration)
            .SetDelay(_drainDelay)
            .SetEase(_drainEase)
            .SetLink(gameObject);
    }

    // 연출 없이 두 바를 즉시 맞춘다. (전투 시작 시 초기화 등)
    public void SetHealthImmediate(float currentHealth, float maxHealth)
    {
        _drainTween?.Kill();
        _fillRatio = CalculateRatio(currentHealth, maxHealth);
        ApplyRatio(_fillImage, _fillRatio);
        SetDelayRatio(_fillRatio);
    }

    // 구독과 동시에 현재 체력으로 맞춘다. 대상이 아직 초기화 전이면(최대 체력 0) 빈 바로 두었다가, 초기화 시 HealthChanged로 채워진다.
    private void SubscribeTarget()
    {
        if (_target == null)
        {
            return;
        }

        _target.HealthChanged += OnTargetHealthChanged;
        SetHealthImmediate(_target.CurrentHealth, _target.MaxHealth);
    }

    private void UnsubscribeTarget()
    {
        if (_target == null)
        {
            return;
        }

        _target.HealthChanged -= OnTargetHealthChanged;
    }

    private void OnTargetHealthChanged()
    {
        SetHealth(_target.CurrentHealth, _target.MaxHealth);
    }

    private static float CalculateRatio(float currentHealth, float maxHealth)
    {
        if (maxHealth <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(currentHealth / maxHealth);
    }

    private float GetDelayRatio()
    {
        return _delayRatio;
    }

    private void SetDelayRatio(float ratio)
    {
        _delayRatio = ratio;
        ApplyRatio(_delayImage, _delayRatio);
    }

    private static void ApplyRatio(Image image, float ratio)
    {
        if (image == null)
        {
            return;
        }

        if (image.type == Image.Type.Filled && image.sprite != null)
        {
            image.fillAmount = ratio;
            return;
        }

        RectTransform rectTransform = image.rectTransform;
        Vector2 anchorMax = rectTransform.anchorMax;
        anchorMax.x = ratio;
        rectTransform.anchorMax = anchorMax;
    }
}

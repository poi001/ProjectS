using System.Collections;
using MoreMountains.Feedbacks;
using UnityEngine;

// SpriteRenderer의 셰이더 float 속성 하나를 정해진 시간 동안 시작 값에서 끝 값으로 바꾼다. (외곽선 투명도·두께 등)
// MaterialPropertyBlock으로 적용하므로 머티리얼 인스턴스를 만들지 않고 머티리얼 에셋의 값도 바꾸지 않는다.
// 시작 값을 피드백에 직접 적어 두므로, 이전에 어떤 값으로 끝났든 재생할 때마다 같은 결과가 나온다.
// 중간에 멈추면 그때의 값에서 멈춘다.
[System.Serializable]
[FeedbackPath("Renderer/Sprite Property Float")]
public class SpritePropertyFloatFeedback : MMF_Feedback
{
    [MMFInspectorGroup("스프라이트 셰이더 속성", true, 0, true)]
    [Tooltip("값을 바꿀 SpriteRenderer입니다.")]
    public SpriteRenderer TargetRenderer;

    [Tooltip("바꿀 셰이더 float 속성의 이름입니다. (예: _OutlineAlpha, _OutlineWidth)")]
    public string PropertyName;

    [Tooltip("재생을 시작할 때의 값입니다.")]
    public float FromValue;

    [Tooltip("곡선 값이 1일 때의 값입니다. 기본 곡선(0에서 1로)에서는 재생이 끝났을 때의 값입니다.")]
    public float ToValue = 1f;

    [Tooltip("시작 값에서 끝 값까지 바뀌는 데 걸리는 시간입니다. 0이면 바로 끝 값이 됩니다. (단위: 초)")]
    public float Duration = 0.2f;

    [Tooltip("시간에 따른 변화 곡선입니다. 가로는 진행도(0~1), 세로 0이 시작 값, 1이 끝 값입니다. 재생이 끝나면 곡선의 마지막 값이 남습니다.")]
    public AnimationCurve Curve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public override float FeedbackDuration
    {
        get
        {
            return ApplyTimeMultiplier(Duration);
        }
        set
        {
            Duration = value;
        }
    }

    private int _propertyId;
    private MaterialPropertyBlock _properties;
    private Coroutine _coroutine;

    protected override void CustomInitialization(MMF_Player owner)
    {
        base.CustomInitialization(owner);
        _propertyId = Shader.PropertyToID(PropertyName);
        _properties = new MaterialPropertyBlock();
    }

    protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1f)
    {
        if (!Active || TargetRenderer == null)
        {
            return;
        }

        StopInterpolation();

        if (FeedbackDuration <= 0f)
        {
            SetValue(Mathf.LerpUnclamped(FromValue, ToValue, Curve.Evaluate(1f)));
            return;
        }

        _coroutine = Owner.StartCoroutine(Interpolate());
    }

    protected override void CustomStopFeedback(Vector3 position, float feedbacksIntensity = 1f)
    {
        StopInterpolation();
    }

    private IEnumerator Interpolate()
    {
        IsPlaying = true;
        float duration = FeedbackDuration;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            SetValue(Mathf.LerpUnclamped(FromValue, ToValue, Curve.Evaluate(elapsed / duration)));
            yield return null;
            elapsed += FeedbackDeltaTime;
        }

        SetValue(Mathf.LerpUnclamped(FromValue, ToValue, Curve.Evaluate(1f)));
        _coroutine = null;
        IsPlaying = false;
    }

    private void StopInterpolation()
    {
        if (_coroutine == null)
        {
            return;
        }

        Owner.StopCoroutine(_coroutine);
        _coroutine = null;
        IsPlaying = false;
    }

    private void SetValue(float value)
    {
        TargetRenderer.GetPropertyBlock(_properties);
        _properties.SetFloat(_propertyId, value);
        TargetRenderer.SetPropertyBlock(_properties);
    }
}

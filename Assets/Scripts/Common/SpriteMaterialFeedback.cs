using MoreMountains.Feedbacks;
using UnityEngine;

// 보관된 머티리얼로 즉시 교체한다. 반복 표시/해제 시 머티리얼 인스턴스를 생성하지 않는다.
[System.Serializable]
[FeedbackPath("Renderer/Sprite Shared Material")]
public class SpriteMaterialFeedback : MMF_Feedback
{
    [MMFInspectorGroup("스프라이트 머티리얼", true, 0, true)]
    [Tooltip("머티리얼을 바꿀 SpriteRenderer입니다.")]
    public SpriteRenderer TargetRenderer;

    [Tooltip("피드백 재생 시 적용할 머티리얼 에셋입니다. 복제하거나 값을 수정하지 않고 그대로 연결합니다.")]
    public Material TargetMaterial;

    public override float FeedbackDuration => 0f;

    protected override void CustomPlayFeedback(Vector3 position, float feedbacksIntensity = 1f)
    {
        if (!Active || TargetRenderer == null || TargetMaterial == null)
        {
            return;
        }

        TargetRenderer.sharedMaterial = TargetMaterial;
    }
}

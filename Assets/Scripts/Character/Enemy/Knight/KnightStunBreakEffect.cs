using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

// 피격 순간의 외형만 복사한다. 섬광과 외피 소멸은 프리팹의 Feel 피드백이 재생하고, 반환은 PooledObject가 담당한다.
[RequireComponent(typeof(PooledObject))]
public class KnightStunBreakEffect : MonoBehaviour
{
    [Tooltip("피격 직전의 Knight 스프라이트를 복사할 렌더러입니다. 본체와 분리되어 있어 스턴 모션을 가리지 않고 외피만 사라집니다.")]
    [SerializeField] private SpriteRenderer _snapshotRenderer;
    [Tooltip("청백색 섬광과 파란 외피 소멸을 재생하는 Feel 피드백입니다. 시간과 강도는 해당 피드백에서 조절합니다.")]
    [SerializeField] private MMF_Player _feedbacks;
    [Tooltip("파란 파편 스프라이트 시트를 한 번 재생하는 플립북입니다.")]
    [SerializeField] private Flipbook _shards;
    [Tooltip("외피의 셰이더 값을 조절하는 Feel 컨트롤러입니다. 복사한 스프라이트의 텍스처를 전달합니다.")]
    [SerializeField] private ShaderController[] _shaderControllers;

    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private static readonly int HitBlendId = Shader.PropertyToID("_HitEffectBlend");
    private static readonly int FadeAmountId = Shader.PropertyToID("_FadeAmount");
    private static readonly int FadeTextureTilingId = Shader.PropertyToID("_FadeTex_ScaleAndTiling");
    private MaterialPropertyBlock _snapshotProperties;

    private void Awake()
    {
        _snapshotProperties = new MaterialPropertyBlock();
    }

    public void Play(SpriteRenderer source, Vector2 hitPoint)
    {
        transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        transform.localScale = source.transform.lossyScale;
        _snapshotRenderer.sprite = source.sprite;
        _snapshotRenderer.flipX = source.flipX;
        _snapshotRenderer.flipY = source.flipY;
        _snapshotRenderer.color = source.color;

        // 풀 재사용 시 지난 소멸 진행도가 남지 않도록 이 인스턴스의 값만 초기화한다.
        Material material = _snapshotRenderer.sharedMaterial;
        _snapshotProperties.Clear();
        _snapshotProperties.SetTexture(MainTextureId, source.sprite.texture);
        _snapshotProperties.SetFloat(HitBlendId, material.GetFloat(HitBlendId));
        _snapshotProperties.SetFloat(FadeAmountId, material.GetFloat(FadeAmountId));
        Rect rect = source.sprite.rect;
        Texture texture = source.sprite.texture;
        _snapshotProperties.SetVector(FadeTextureTilingId, new Vector4(texture.width / rect.width, texture.height / rect.height,
            -rect.x / rect.width, -rect.y / rect.height));
        _snapshotRenderer.SetPropertyBlock(_snapshotProperties, 0);

        for (int i = 0; i < _shaderControllers.Length; i++)
        {
            _shaderControllers[i].StoreSpriteRendererTexture();
        }

        _shards.transform.position = new Vector3(hitPoint.x, hitPoint.y, source.transform.position.z);
        _shards.gameObject.SetActive(true);
        _shards.Play();
        _feedbacks.PlayFeedbacks();
    }

    private void OnDisable()
    {
        if (_feedbacks != null)
        {
            _feedbacks.StopFeedbacks();
        }
    }
}

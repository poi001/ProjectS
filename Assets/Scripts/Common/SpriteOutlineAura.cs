using UnityEngine;

// 캐릭터 몸 주위의 일렁이는 오라. 본체 SpriteRenderer와 같은 그림을 그대로 따라 그리는 별도의 SpriteRenderer에 붙인다.
// 머티리얼은 All In 1 Sprite Shader의 외곽선(Outline)에 외곽선 왜곡(Outline Distortion), "외곽선만 그리기", Atlas, Clipping을 켠 것을 쓴다.
// 본체의 머티리얼을 건드리지 않으므로 본체의 머티리얼 교체(기절 가능 표시 등)와 겹쳐도 서로 영향을 주지 않는다.
// 하는 일은 두 가지다.
// 1. 매 프레임 본체의 스프라이트·좌우 반전·위치를 그대로 따라간다. (Animator가 스프라이트를 바꾼 뒤인 LateUpdate에서)
// 2. 스프라이트가 바뀔 때마다 셰이더 값을 그 스프라이트에 맞춰 다시 계산한다.
//    - 시트 안에서 이 스프라이트가 차지하는 칸(UV 범위)을 알려 주고, 칸 가장자리를 조금 잘라 낸다.
//      외곽선은 주변 픽셀을 살펴 그리므로, 자르지 않으면 시트의 옆 칸 그림(위 칸의 발 등)이 칸 가장자리에 띠로 보인다.
//    - All In 1의 외곽선 왜곡은 시트 전체를 기준(UV)으로 계산해서, 같은 값이라도 시트 크기가 다르면 일렁이는 정도가 달라진다.
//      스프라이트 픽셀 기준 값으로 바꿔 넣어 어느 애니메이션에서나 비슷하게 보이게 한다. (가로·세로 비율이 다른 시트는 약간 차이가 남는다)
// 값은 MaterialPropertyBlock으로 넣으므로 머티리얼 인스턴스를 만들지 않고 머티리얼 에셋도 바뀌지 않는다.
// 오라가 본체 밖으로 퍼지려면 스프라이트의 Mesh Type이 Full Rect여야 한다. (Tight면 몸 가까이에서 잘린다)
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteOutlineAura : MonoBehaviour
{
    private static readonly int MainTextureId = Shader.PropertyToID("_MainTex");
    private static readonly int MinXUvId = Shader.PropertyToID("_MinXUV");
    private static readonly int MaxXUvId = Shader.PropertyToID("_MaxXUV");
    private static readonly int MinYUvId = Shader.PropertyToID("_MinYUV");
    private static readonly int MaxYUvId = Shader.PropertyToID("_MaxYUV");
    private static readonly int ClipLeftId = Shader.PropertyToID("_ClipUvLeft");
    private static readonly int ClipRightId = Shader.PropertyToID("_ClipUvRight");
    private static readonly int ClipUpId = Shader.PropertyToID("_ClipUvUp");
    private static readonly int ClipDownId = Shader.PropertyToID("_ClipUvDown");
    private static readonly int DistortAmountId = Shader.PropertyToID("_OutlineDistortAmount");
    private static readonly int DistortTextureScaleOffsetId = Shader.PropertyToID("_OutlineDistortTex_ST");
    private static readonly int DistortSpeedXId = Shader.PropertyToID("_OutlineDistortTexXSpeed");
    private static readonly int DistortSpeedYId = Shader.PropertyToID("_OutlineDistortTexYSpeed");

    // 셰이더가 왜곡 텍스처 값(-0.5~0.5)에 곱하는 고정 배율
    private const float ShaderDistortScale = 0.2f;
    // 셰이더가 스크롤에 쓰는 시간(_Time.x)은 실제 시간의 1/20이다.
    private const float ShaderTimeScale = 20f;

    [Header("대상")]
    [Tooltip("따라 그릴 본체 SpriteRenderer입니다. 이 렌더러의 스프라이트·좌우 반전·위치를 매 프레임 그대로 가져옵니다.")]
    [SerializeField] private SpriteRenderer _source;

    [Header("일렁임")]
    [Tooltip("오라 가장자리가 일렁이며 안팎으로 움직이는 최대 폭입니다. (단위: 스프라이트 픽셀) 0이면 일렁이지 않는 고른 외곽선이 됩니다.")]
    [SerializeField, Min(0f)] private float _distortionPixels = 1f;

    [Tooltip("일렁임 무늬(왜곡 텍스처) 한 장이 덮는 크기입니다. (단위: 스프라이트 픽셀) 작을수록 무늬가 잘게 쪼개지고, 클수록 크게 출렁입니다.")]
    [SerializeField, Min(1f)] private float _noiseSpanPixels = 62f;

    [Tooltip("일렁임 무늬가 흘러가는 속도입니다. X는 가로, Y는 세로이며 1이면 1초에 무늬 한 장만큼 움직입니다. (게임 시간 기준)")]
    [SerializeField] private Vector2 _noiseScrollSpeed = new Vector2(0.15f, 0.45f);

    [Header("칸 가장자리 자르기")]
    [Tooltip("스프라이트 칸의 왼쪽·오른쪽·위쪽 가장자리에서 오라를 그리지 않는 폭입니다. (단위: 스프라이트 픽셀) 시트의 옆 칸 그림이 가장자리에 띠로 보이는 것을 막습니다.\n오라가 가장 넓게 퍼질 때의 두께(외곽선 두께 + 일렁임)보다 크게 둡니다. 아래쪽은 발밑 오라가 잘리지 않도록 자르지 않습니다.")]
    [SerializeField, Min(0f)] private float _edgeClipPixels = 6f;

    private SpriteRenderer _renderer;
    private MaterialPropertyBlock _properties;
    private Sprite _boundSprite;

    private void Awake()
    {
        _renderer = GetComponent<SpriteRenderer>();
        _properties = new MaterialPropertyBlock();
    }

    // 꺼져 있는 동안 본체의 스프라이트가 바뀌었을 수 있으므로, 켜질 때 셰이더 값을 다시 계산하게 한다.
    private void OnEnable()
    {
        _boundSprite = null;
    }

    private void LateUpdate()
    {
        if (_source == null)
        {
            return;
        }

        Sprite sprite = _source.sprite;

        if (_renderer.sprite != sprite)
        {
            _renderer.sprite = sprite;
        }

        _renderer.flipX = _source.flipX;
        _renderer.flipY = _source.flipY;
        transform.position = _source.transform.position;

        if (sprite == null || _boundSprite == sprite)
        {
            return;
        }

        ApplySpriteProperties(sprite);
        _boundSprite = sprite;
    }

    // 스프라이트의 칸 위치와 시트 크기에 맞춰 칸 범위·가장자리 자르기·왜곡 세기·무늬 크기·흐르는 속도를 계산해 넣는다. 스프라이트가 바뀔 때만 호출된다.
    private void ApplySpriteProperties(Sprite sprite)
    {
        Texture texture = sprite.texture;
        Rect rect = sprite.rect;
        float textureWidth = texture.width;
        float textureHeight = texture.height;

        // Atlas를 켠 셰이더는 무늬 좌표를 칸 기준(0~1)으로 계산하므로, 칸 크기로 반복 수를 정한다.
        float tilingX = rect.width / _noiseSpanPixels;
        float tilingY = rect.height / _noiseSpanPixels;

        // 셰이더는 왜곡을 시트 UV 단위로 가로·세로에 똑같이 더한다. 시트 가로·세로 크기의 기하 평균으로 나눠 픽셀 폭에 가깝게 맞춘다.
        float distortAmount = _distortionPixels / (ShaderDistortScale * 0.5f * Mathf.Sqrt(textureWidth * textureHeight));

        _renderer.GetPropertyBlock(_properties);
        _properties.SetTexture(MainTextureId, texture);
        _properties.SetFloat(MinXUvId, rect.xMin / textureWidth);
        _properties.SetFloat(MaxXUvId, rect.xMax / textureWidth);
        _properties.SetFloat(MinYUvId, rect.yMin / textureHeight);
        _properties.SetFloat(MaxYUvId, rect.yMax / textureHeight);
        _properties.SetFloat(ClipLeftId, _edgeClipPixels / rect.width);
        _properties.SetFloat(ClipRightId, _edgeClipPixels / rect.width);
        _properties.SetFloat(ClipUpId, _edgeClipPixels / rect.height);
        _properties.SetFloat(ClipDownId, 0f);
        _properties.SetFloat(DistortAmountId, distortAmount);
        _properties.SetVector(DistortTextureScaleOffsetId, new Vector4(tilingX, tilingY, 0f, 0f));
        // 셰이더는 스크롤 값에 무늬 반복 수를 곱하므로, 반복 수로 나눠 칸 크기와 관계없이 같은 속도로 흐르게 한다.
        _properties.SetFloat(DistortSpeedXId, _noiseScrollSpeed.x * ShaderTimeScale / tilingX);
        _properties.SetFloat(DistortSpeedYId, _noiseScrollSpeed.y * ShaderTimeScale / tilingY);
        _renderer.SetPropertyBlock(_properties);
    }
}

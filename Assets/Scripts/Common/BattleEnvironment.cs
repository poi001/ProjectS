using UnityEngine;

// 전투 씬의 배경·바닥·벽 스프라이트를 한곳에서 정한다. 템플릿 씬에서는 이 컴포넌트의 스프라이트 슬롯 3개만 채우면 된다.
//  - 바닥·벽: 타일 방식(Tiled) 렌더러에 스프라이트만 바꿔 넣고, 렌더러에 정해 둔 크기(Size)는 유지한다.
//  - 배경: 같은 스프라이트를 3x3 타일 9장에 모두 넣고, 카메라가 보여 주는 화면 크기에 맞춰 크기와 위치를 계산한다.
//    가운데 타일이 화면을 덮고 주변 8장은 틈과 겹침 없이 붙어서, 카메라 흔들림이나 줌 이동 때 가장자리가 보이지 않는다.
// 인스펙터 값을 바꾸면 바로 반영되고(OnValidate), 플레이 시작 때도 한 번 맞춘다. 매 프레임 하는 일은 없다.
// 카메라의 Orthographic Size나 목표 비율을 바꿨다면 컨텍스트 메뉴 "환경 다시 맞추기"를 실행한다.
// 배경 타일 위치는 각 타일의 부모 기준 로컬 좌표이므로, 타일의 부모는 카메라 중심(보통 0, 0)에 두고 타일의 Z로 깊이를 정한다.
public class BattleEnvironment : MonoBehaviour
{
    private const int BackgroundTilesPerSide = 3;
    private const int BackgroundTileCount = BackgroundTilesPerSide * BackgroundTilesPerSide;
    private const float DefaultAspect = 16f / 9f;
    // 타일 위치·스케일이 이 값(제곱 거리)보다 적게 달라지면 같은 값으로 본다.
    private const float PositionTolerance = 0.000001f;

    [Header("스프라이트")]
    [Tooltip("배경 스프라이트입니다. 배경 타일 9장에 모두 적용됩니다.")]
    [SerializeField] private Sprite _backgroundSprite;

    [Tooltip("바닥 스프라이트입니다. 타일 방식으로 반복해서 깔리며, 바닥 렌더러에 정해 둔 크기는 그대로 유지됩니다.")]
    [SerializeField] private Sprite _groundSprite;

    [Tooltip("벽 스프라이트입니다. 타일 방식으로 반복해서 깔리며, 왼쪽·오른쪽 벽 렌더러에 같이 적용됩니다.")]
    [SerializeField] private Sprite _wallSprite;

    [Header("배경 배치")]
    [Tooltip("배경이 덮어야 할 화면 크기를 계산하는 기준 카메라(메인 카메라)입니다. Orthographic Size와, 같은 오브젝트의 CameraAspectFitter가 있으면 그 목표 비율을 씁니다. 없으면 16:9로 계산합니다.")]
    [SerializeField] private Camera _camera;

    [Tooltip("켜면 배경 스프라이트의 원본 비율을 유지하면서 화면을 덮고, 넘치는 부분은 잘립니다. 끄면 비율을 무시하고 화면에 맞게 늘려서 덮습니다. 배경 그림이 16:9가 아닐 때 켜 두는 것을 권장합니다.")]
    [SerializeField] private bool _keepBackgroundAspect = true;

    [Tooltip("가운데 배경 타일이 화면보다 얼마나 더 크게 덮을지 정하는 배율입니다. 1이면 화면과 딱 맞고, 1.05면 5% 더 큽니다. 카메라 흔들림 때 가장자리가 보이지 않도록 1보다 크게 둡니다.")]
    [SerializeField, Min(1f)] private float _backgroundOverscan = 1.05f;

    [Header("대상 렌더러")]
    [Tooltip("배경 타일 9장입니다. 순서는 왼쪽 아래에서 시작해 오른쪽으로, 다음 줄은 위로 올라갑니다. (0=왼쪽 아래, 4=가운데, 8=오른쪽 위)")]
    [SerializeField] private SpriteRenderer[] _backgroundTiles;

    [Tooltip("바닥 스프라이트를 적용할 렌더러들입니다. 타일 방식(Draw Mode: Tiled)이어야 합니다.")]
    [SerializeField] private SpriteRenderer[] _groundRenderers;

    [Tooltip("벽 스프라이트를 적용할 렌더러들입니다. 타일 방식(Draw Mode: Tiled)이어야 합니다.")]
    [SerializeField] private SpriteRenderer[] _wallRenderers;

    // Awake에서는 렌더러 값을 바꾸면 SendMessage 경고가 나므로 Start에서 맞춘다. (첫 프레임이 그려지기 전에 끝난다)
    private void Start()
    {
        if (_camera == null)
        {
            Debug.LogWarning("BattleEnvironment의 기준 카메라가 지정되지 않아 배경 크기와 위치를 맞추지 못합니다.", this);
        }

        Apply();
    }

#if UNITY_EDITOR
    // OnValidate 안에서 렌더러·트랜스폼을 바꾸면 SendMessage 경고가 나므로, 에디터의 다음 틱에 적용한다.
    private void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyDelayed;
        UnityEditor.EditorApplication.delayCall += ApplyDelayed;
    }

    private void ApplyDelayed()
    {
        UnityEditor.EditorApplication.delayCall -= ApplyDelayed;

        // 그 사이 오브젝트가 삭제되었거나 씬이 닫혔다면 건너뛴다.
        if (this == null)
        {
            return;
        }

        Apply();
    }
#endif

    // 스프라이트를 모든 렌더러에 적용하고 배경 타일의 크기와 위치를 맞춘다.
    [ContextMenu("환경 다시 맞추기")]
    public void Apply()
    {
        ApplyTiledSprite(_groundRenderers, _groundSprite);
        ApplyTiledSprite(_wallRenderers, _wallSprite);
        ApplyBackground();
    }

    private static void ApplyTiledSprite(SpriteRenderer[] spriteRenderers, Sprite sprite)
    {
        if (spriteRenderers == null)
        {
            return;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];

            if (spriteRenderer == null || spriteRenderer.sprite == sprite)
            {
                continue;
            }

            // 타일 방식은 스프라이트를 바꾸면 크기가 새 스프라이트 크기로 초기화될 수 있어, 원래 크기를 되돌린다.
            Vector2 size = spriteRenderer.size;
            spriteRenderer.sprite = sprite;

            if (spriteRenderer.drawMode != SpriteDrawMode.Simple)
            {
                spriteRenderer.size = size;
            }
        }
    }

    private void ApplyBackground()
    {
        if (_backgroundTiles == null || _backgroundTiles.Length != BackgroundTileCount)
        {
            return;
        }

        for (int i = 0; i < BackgroundTileCount; i++)
        {
            if (_backgroundTiles[i] != null && _backgroundTiles[i].sprite != _backgroundSprite)
            {
                _backgroundTiles[i].sprite = _backgroundSprite;
            }
        }

        if (_backgroundSprite == null || _camera == null)
        {
            return;
        }

        // 스프라이트의 원래 월드 크기(스케일 1 기준).
        Vector2 spriteSize = _backgroundSprite.bounds.size;

        if (spriteSize.x <= 0f || spriteSize.y <= 0f)
        {
            return;
        }

        float viewHeight = _camera.orthographicSize * 2f;
        float viewWidth = viewHeight * GetViewAspect();
        float scaleX = viewWidth * _backgroundOverscan / spriteSize.x;
        float scaleY = viewHeight * _backgroundOverscan / spriteSize.y;

        if (_keepBackgroundAspect)
        {
            float scale = Mathf.Max(scaleX, scaleY);
            scaleX = scale;
            scaleY = scale;
        }

        Vector2 tileSize = new Vector2(spriteSize.x * scaleX, spriteSize.y * scaleY);

        for (int row = 0; row < BackgroundTilesPerSide; row++)
        {
            for (int column = 0; column < BackgroundTilesPerSide; column++)
            {
                SpriteRenderer tile = _backgroundTiles[row * BackgroundTilesPerSide + column];

                if (tile == null)
                {
                    continue;
                }

                // 가운데(1, 1)를 기준으로 타일 한 장 크기만큼씩 떨어뜨려 틈과 겹침 없이 붙인다. Z(깊이)는 그대로 둔다.
                // 값이 이미 같으면 건드리지 않는다. (불필요한 변경 알림과 씬 변경 표시를 막는다)
                Transform tileTransform = tile.transform;
                Vector3 position = tileTransform.localPosition;
                position.x = (column - 1) * tileSize.x;
                position.y = (row - 1) * tileSize.y;
                Vector3 scale = new Vector3(scaleX, scaleY, 1f);

                if ((tileTransform.localPosition - position).sqrMagnitude > PositionTolerance)
                {
                    tileTransform.localPosition = position;
                }

                if ((tileTransform.localScale - scale).sqrMagnitude > PositionTolerance)
                {
                    tileTransform.localScale = scale;
                }
            }
        }
    }

    private float GetViewAspect()
    {
        if (_camera.TryGetComponent(out CameraAspectFitter fitter))
        {
            return fitter.TargetAspect;
        }

        return DefaultAspect;
    }
}

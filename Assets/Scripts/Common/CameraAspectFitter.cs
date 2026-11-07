using UnityEngine;
using UnityEngine.Rendering;

// 카메라 뷰포트를 목표 비율(기본 16:9)로 고정하고, 남는 영역은 검은 띠(레터박스/필러박스)로 채운다.
// 전투 씬은 필드 전체를 한 카메라에 담으므로, 해상도 비율이 달라져도 보이는 범위가 같아야
// 양쪽 벽/바닥/배경을 고정 위치에 둘 수 있다.
// 검은 띠는 뷰포트 밖을 지우는 별도의 레터박스 카메라가 그린다. (URP는 뷰포트 밖을 자동으로 지우지 않는다)
// Play 중에만 동작하므로 편집 모드의 Game 뷰에서는 검은 띠가 적용되지 않는다.
[RequireComponent(typeof(Camera))]
public class CameraAspectFitter : MonoBehaviour
{
    [Header("목표 비율")]
    [Tooltip("목표 화면 비율의 가로 값입니다. 가로:세로 비율을 계산하는 데 쓰입니다. (기본 16:9의 16)")]
    [SerializeField, Min(0.01f)] private float _targetWidth = 16f;

    [Tooltip("목표 화면 비율의 세로 값입니다. 가로:세로 비율을 계산하는 데 쓰입니다. (기본 16:9의 9)")]
    [SerializeField, Min(0.01f)] private float _targetHeight = 9f;

    [Header("레터박스")]
    [Tooltip("검은 띠 영역을 그리는 카메라입니다. 메인 카메라보다 Depth가 낮고, Clear Flags가 Solid Color(검정), Culling Mask가 Nothing이어야 합니다.")]
    [SerializeField] private Camera _letterboxCamera;

    [Header("UI")]
    [Tooltip("UI를 그리는 카메라입니다. 줌·흔들림의 영향을 받지 않도록 메인 카메라와 따로 두며, 같은 뷰포트로 함께 맞춰집니다. 비워 두면 메인 카메라만 맞춥니다.")]
    [SerializeField] private Camera _uiCamera;

    private Camera _camera;
    private int _lastScreenWidth;
    private int _lastScreenHeight;

    // 목표 화면 비율(가로/세로). 배경처럼 카메라가 보여 주는 영역의 크기를 계산해야 하는 곳에서 읽는다.
    public float TargetAspect => _targetWidth / _targetHeight;

    private void Awake()
    {
        _camera = GetComponent<Camera>();

        if (_letterboxCamera == null)
        {
            Debug.LogWarning("CameraAspectFitter의 레터박스 카메라가 지정되지 않았습니다. 검은 띠 영역이 지워지지 않을 수 있습니다.", this);
        }
    }

    private void OnEnable()
    {
        // 창 크기가 바뀐 것을 카메라가 렌더링되는 시점에 검사한다.
        RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        Fit();
    }

    private void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;

        // 컴포넌트가 꺼지거나 제거되면 뷰포트를 원래대로 되돌린다.
        if (_camera != null)
        {
            SetViewport(new Rect(0f, 0f, 1f, 1f));
        }
    }

    private void OnBeginCameraRendering(ScriptableRenderContext context, Camera renderingCamera)
    {
        if (renderingCamera != _camera)
        {
            return;
        }

        // 창 크기/해상도가 바뀐 프레임에만 다시 계산한다.
        if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
        {
            Fit();
        }
    }

    public void Fit()
    {
        _lastScreenWidth = Screen.width;
        _lastScreenHeight = Screen.height;

        if (_lastScreenWidth <= 0 || _lastScreenHeight <= 0)
        {
            return;
        }

        ApplyAspect((float)_lastScreenWidth / _lastScreenHeight);
    }

    // screenAspect(가로/세로)에 맞춰 뷰포트를 목표 비율로 맞춘다.
    public void ApplyAspect(float screenAspect)
    {
        if (_camera == null)
        {
            _camera = GetComponent<Camera>();
        }

        float targetAspect = _targetWidth / _targetHeight;
        Rect viewport;

        if (screenAspect > targetAspect)
        {
            // 화면이 더 넓다 → 좌우에 검은 띠
            float width = targetAspect / screenAspect;
            viewport = new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }
        else
        {
            // 화면이 더 좁다 → 위아래에 검은 띠
            float height = screenAspect / targetAspect;
            viewport = new Rect(0f, (1f - height) * 0.5f, 1f, height);
        }

        SetViewport(viewport);
    }

    private void SetViewport(Rect viewport)
    {
        _camera.rect = viewport;

        if (_uiCamera != null)
        {
            _uiCamera.rect = viewport;
        }
    }
}

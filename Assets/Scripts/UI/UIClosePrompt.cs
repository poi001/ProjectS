using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;

// "키를 누르거나 화면을 클릭하면 창이 닫힌다"는 안내 글과, 그 입력으로 창을 닫는 기능을 묶은 공용 컴포넌트.
// 닫는 방법이 "아무 데나 누르기"뿐인 창(획득 창 등)의 Content 아래에 UIClosePrompt 프리팹을 넣고 Window에 그 창을 연결해 쓴다.
//  - 안내 글: 켜져 있는 동안 CanvasGroup의 투명도를 오르내려 깜빡인다. 문구는 이 오브젝트의 글자(TextMeshPro)에서 바꾼다.
//  - 닫기: 정해 둔 키(기본 Z)를 누르거나 화면 아무 곳이나 왼쪽 클릭하면 CloseRequested를 알리고 연결한 창을 닫는다.
// 창의 Content가 켜질 때 함께 켜지고 꺼질 때 함께 꺼지므로, 창이 열려 있는 동안에만 입력을 받는다.
// 창이 게임을 일시정지해도 동작하도록 깜빡임과 입력 대기는 실제 시간으로 센다.
public class UIClosePrompt : MonoBehaviour
{
    [Header("닫을 창")]
    [Tooltip("입력을 받으면 닫을 창입니다. 비워 두면 닫지 않고 CloseRequested 이벤트만 알립니다.")]
    [SerializeField] private UIWindow _window;

    [Header("입력")]
    [Tooltip("누르면 창을 닫는 키입니다. 안내 글의 문구와 맞춥니다.")]
    [SerializeField] private Key _closeKey = Key.Z;
    [Tooltip("켜면 화면 아무 곳이나 마우스 왼쪽 버튼으로 눌러도 창을 닫습니다.")]
    [SerializeField] private bool _closesOnClick = true;
    [Tooltip("창이 열린 뒤 이 시간 동안은 닫는 입력을 받지 않습니다. 창이 뜨기 직전에 누르고 있던 입력으로 바로 닫히는 것을 막습니다. 실제 시간 기준입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _inputDelay = 0.3f;

    [Header("안내 글 깜빡임")]
    [Tooltip("안내 글의 투명도를 바꿀 CanvasGroup입니다. 이 오브젝트의 CanvasGroup을 연결합니다.")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [Tooltip("안내 글이 다 보이는 상태에서 가장 흐린 상태까지(또는 그 반대로) 한 번 바뀌는 데 걸리는 시간입니다. 실제 시간 기준입니다. (단위: 초)")]
    [SerializeField, Min(0.05f)] private float _blinkDuration = 0.8f;
    [Tooltip("깜빡일 때 가장 흐려졌을 때의 투명도입니다. 0이면 완전히 사라졌다가 나타납니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _minAlpha;

    private Tween _blinkTween;
    // 켜진 시각 (실제 시간). 입력 대기 시간을 세는 기준이다.
    private float _enabledTime;

    // 닫는 입력을 받았을 때 알린다. 연결한 창을 닫기 직전에 호출된다.
    public event Action CloseRequested;

    private void OnEnable()
    {
        _enabledTime = Time.unscaledTime;

        if (_canvasGroup == null)
        {
            return;
        }

        // 다 보이는 상태에서 시작해 흐려졌다 돌아오기를 반복한다.
        _canvasGroup.alpha = 1f;
        _blinkTween = _canvasGroup.DOFade(_minAlpha, _blinkDuration)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true)
            .SetLink(gameObject);
    }

    private void OnDisable()
    {
        if (_blinkTween != null)
        {
            _blinkTween.Kill();
            _blinkTween = null;
        }
    }

    private void Update()
    {
        if (Time.unscaledTime - _enabledTime < _inputDelay || !WasClosePressed())
        {
            return;
        }

        CloseRequested?.Invoke();

        if (_window != null)
        {
            _window.Close();
        }
    }

    private bool WasClosePressed()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard != null && _closeKey != Key.None && keyboard[_closeKey].wasPressedThisFrame)
        {
            return true;
        }

        Mouse mouse = Mouse.current;
        return _closesOnClick && mouse != null && mouse.leftButton.wasPressedThisFrame;
    }
}

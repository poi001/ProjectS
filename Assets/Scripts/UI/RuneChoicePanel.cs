using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 룬 선택 창의 패널 하나. 위에서부터 아이콘, 이름, 갈래 표시(공격·방어·보조), 설명창이 있고 패널 전체가 버튼이다.
// RuneSelectWindow가 Setup으로 룬을 채우고 Picked를 구독한다. 누르면(클릭 또는 Enter) 그 룬으로 Picked를 알린다.
// 마우스를 올리면 그 패널을 선택 상태로 바꿔, 키보드 선택과 마우스 강조가 한 패널에만 보이게 한다.
// 연출(DOTween, 일시정지 중에 재생되므로 실제 시간으로 보간):
//  - 마우스를 올리거나 키보드(←/→)로 선택을 옮겨 오면 좌우로 빠르게 아주 약간 기울어진다.
//  - 좌클릭을 누르면 살짝 작아지고, 떼면 원래 크기로 돌아온다. 패널 위에서 뗐다면 원래 크기로 돌아온 뒤에 룬 선택이 확정된다.
//  - 누른 채로 마우스가 패널 밖으로 나가면 눌림 연출(작아진 크기, 눌린 색)을 모두 취소하고 누르기 전 모습으로 돌아온다. (밖에서 떼면 고르지 않는다)
//    누른 채로 다시 패널 위로 돌아오면 다시 눌린 모습이 된다. (그대로 떼면 고르게 되므로 눌린 모습을 다시 보여 준다)
//  - Enter로 고르면 작아졌다가 돌아오는 연출을 이어서 보여 준 뒤 확정된다.
// 연출은 자식 Visual만 움직인다. 클릭 판정을 받는 패널 루트는 움직이지 않아, 기울거나 작아져도 마우스가 패널 밖으로 벗어나지 않는다.
public class RuneChoicePanel : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler
{
    [Header("참조")]
    [Tooltip("패널 전체를 덮는 버튼입니다. 패널 루트에 있으며 클릭 판정을 받습니다. 선택·강조 색은 이 버튼의 Color Tint로 정합니다.")]
    [SerializeField] private Button _button;
    [Tooltip("룬 아이콘 이미지입니다. 룬에 아이콘이 없으면 이 이미지의 색(회색)으로만 보입니다.")]
    [SerializeField] private Image _iconImage;
    [Tooltip("룬 이름 글자입니다.")]
    [SerializeField] private TMP_Text _nameText;
    [Tooltip("갈래 표시의 배경 이미지입니다. 갈래에 따라 아래 색으로 바뀝니다.")]
    [SerializeField] private Image _categoryBackground;
    [Tooltip("갈래 표시의 글자입니다. 갈래에 따라 아래 문구로 바뀝니다.")]
    [SerializeField] private TMP_Text _categoryText;
    [Tooltip("룬 효과 설명 글자입니다.")]
    [SerializeField] private TMP_Text _descriptionText;

    [Header("갈래 표시")]
    [Tooltip("공격 룬의 갈래 문구입니다. 한글 TMP 폰트가 생기기 전까지는 영문으로 둡니다.")]
    [SerializeField] private string _attackLabel = "ATTACK";
    [Tooltip("공격 룬의 갈래 표시 색입니다. (빨강 계열)")]
    [SerializeField] private Color _attackColor = new Color(0.75f, 0.2f, 0.2f, 1f);
    [Tooltip("방어 룬의 갈래 문구입니다.")]
    [SerializeField] private string _defenseLabel = "DEFENSE";
    [Tooltip("방어 룬의 갈래 표시 색입니다. (파랑 계열)")]
    [SerializeField] private Color _defenseColor = new Color(0.2f, 0.4f, 0.8f, 1f);
    [Tooltip("보조 룬의 갈래 문구입니다.")]
    [SerializeField] private string _supportLabel = "SUPPORT";
    [Tooltip("보조 룬의 갈래 표시 색입니다. (초록 계열)")]
    [SerializeField] private Color _supportColor = new Color(0.2f, 0.6f, 0.3f, 1f);

    [Header("연출")]
    [Tooltip("기울임·눌림 연출로 움직이는 오브젝트입니다. 패널의 그림 전체를 담은 자식 Visual을 연결합니다. 비워 두면 연출 없이 동작합니다.")]
    [SerializeField] private RectTransform _visual;
    [Tooltip("마우스를 올리거나 키보드로 선택을 옮겨 왔을 때 좌우로 기울어지는 최대 각도입니다. (단위: 도)")]
    [SerializeField, Min(0f)] private float _tiltAngle = 3f;
    [Tooltip("좌우로 기울어지는 횟수입니다. 한쪽으로 기울었다가 반대쪽으로 기울고 돌아오는 것이 1번입니다.")]
    [SerializeField, Min(1)] private int _tiltCount = 3;
    [Tooltip("기울임 연출 전체에 걸리는 시간입니다. 실제 시간 기준이라 일시정지 중에도 재생됩니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _tiltDuration = 0.36f;
    [Tooltip("누르고 있는 동안의 크기 배율입니다. (1 = 원래 크기, 0.95 = 5% 작아짐)")]
    [SerializeField, Range(0.5f, 1f)] private float _pressedScale = 0.95f;
    [Tooltip("눌렀을 때 작아지는 데 걸리는 시간입니다. (단위: 초, 실제 시간)")]
    [SerializeField, Min(0f)] private float _pressDuration = 0.06f;
    [Tooltip("뗐을 때 원래 크기로 돌아오는 데 걸리는 시간입니다. 패널 위에서 뗐다면 이 연출이 끝난 뒤 룬 선택이 확정됩니다. (단위: 초, 실제 시간)")]
    [SerializeField, Min(0f)] private float _releaseDuration = 0.1f;

    private PassiveRuneDataSO _rune;
    private Tween _tiltTween;
    private Tween _scaleTween;
    // 기울임 보간 콜백. 연출을 재생할 때마다 새로 만들지 않도록 한 번만 만들어 둔다.
    private TweenCallback<float> _applyTilt;
    // 고른 뒤 원래 크기로 돌아오는 연출 중인지 여부. 이 동안에는 다시 누르거나 기울어지지 않는다.
    private bool _isConfirming;
    // 입력 잠금 여부. 어느 패널이든 확정 연출이 시작되면 창이 모든 패널을 잠가, 그 사이에 다른 패널이 반응하지 않게 한다.
    private bool _isInputLocked;
    // 이 패널 위에서 좌클릭을 눌러 아직 떼지 않았는지 여부. 마우스가 패널 밖으로 나갔다 들어올 때 눌림 연출을 취소·재개하는 데 쓴다.
    private bool _isPointerHeld;
    // 버튼에 눌림 취소·재개를 알릴 때 쓰는 이벤트 데이터. 매번 새로 만들지 않도록 한 번만 만들어 둔다. (SetButtonPressedLook)
    private PointerEventData _pressLookEvent;

    // 이 패널을 골라 확정 연출(원래 크기로 돌아오기)이 시작됐을 때 알린다. 인자는 이 패널이다.
    public event Action<RuneChoicePanel> PickStarted;

    // 확정 연출이 끝나 룬 선택이 확정됐을 때 알린다. 인자는 이 패널에 채운 룬이다.
    public event Action<PassiveRuneDataSO> Picked;

    public Button Button => _button;

    private void OnEnable()
    {
        if (_button != null)
        {
            _button.onClick.AddListener(OnButtonClicked);
        }
    }

    private void OnDisable()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClicked);
        }

        // 창이 닫혀 패널이 꺼질 때 트윈이 남지 않게 하고, 다음에 열릴 때 기울거나 작아진 채로 나오지 않게 한다.
        ResetEffects();
    }

    // 룬 정보로 패널을 채운다. 진행 중이던 연출은 멈추고 원래 모습으로 되돌린다.
    public void Setup(PassiveRuneDataSO rune)
    {
        _rune = rune;
        ResetEffects();

        if (_iconImage != null)
        {
            _iconImage.sprite = rune.Icon;
        }

        if (_nameText != null)
        {
            _nameText.text = rune.RuneName;
        }

        if (_descriptionText != null)
        {
            _descriptionText.text = rune.Description;
        }

        if (_categoryText != null)
        {
            _categoryText.text = GetCategoryLabel(rune.Category);
        }

        if (_categoryBackground != null)
        {
            _categoryBackground.color = GetCategoryColor(rune.Category);
        }
    }

    // 마우스 올림·누름·선택 이동에 반응하지 않게 잠그거나 푼다. 진행 중인 연출은 그대로 이어진다.
    public void SetInputLocked(bool isLocked)
    {
        _isInputLocked = isLocked;
    }

    // 진행 중인 연출을 멈추고 기울기·크기를 원래대로 되돌린다. 입력 잠금도 푼다.
    public void ResetEffects()
    {
        KillTiltTween();
        KillScaleTween();
        _isConfirming = false;
        _isInputLocked = false;
        _isPointerHeld = false;

        if (_visual != null)
        {
            _visual.localRotation = Quaternion.identity;
            _visual.localScale = Vector3.one;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (_button == null || !_button.interactable || _isInputLocked)
        {
            return;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(_button.gameObject);
        }

        // 누른 채로 밖에 나갔다가 돌아온 경우: 기울이지 않고 눌린 모습(크기·색)으로 되돌린다.
        if (_isPointerHeld)
        {
            if (CanPlayPressEffect())
            {
                PlayScale(_pressedScale, _pressDuration);
                SetButtonPressedLook(true);
            }

            return;
        }

        PlayTilt();
    }

    // 누른 채로 마우스가 패널 밖으로 나가면 눌림 연출(크기·색)을 모두 취소하고 누르기 전 모습으로 돌아온다.
    public void OnPointerExit(PointerEventData eventData)
    {
        if (_isPointerHeld && CanPlayPressEffect())
        {
            PlayScale(1f, _releaseDuration);
            SetButtonPressedLook(false);
        }
    }

    // 버튼(Selectable)은 누르고 있는 동안에는 마우스가 밖으로 나가도 눌린 상태(눌린 색)를 유지한다.
    // 눌림 취소·재개에 맞춰 버튼에 "뗐다 / 다시 눌렀다"를 직접 알려, 버튼의 색(Color Tint)도 함께 바뀌게 한다.
    // 클릭 판정은 입력 모듈(EventSystem)이 따로 하므로, 여기서 알리는 것은 버튼의 모습에만 영향을 준다.
    private void SetButtonPressedLook(bool isPressed)
    {
        if (_button == null || EventSystem.current == null)
        {
            return;
        }

        if (_pressLookEvent == null)
        {
            _pressLookEvent = new PointerEventData(EventSystem.current);
        }

        _pressLookEvent.button = PointerEventData.InputButton.Left;

        if (isPressed)
        {
            _button.OnPointerDown(_pressLookEvent);
        }
        else
        {
            _button.OnPointerUp(_pressLookEvent);
        }
    }

    // 키보드(←/→)로 선택이 옮겨 왔을 때만 기울인다. 마우스를 올려서 선택된 경우는 OnPointerEnter가 이미 기울였고,
    // 창이 열릴 때의 첫 선택이나 풀린 선택을 되돌리는 경우(코드로 선택)에는 기울이지 않는다.
    public void OnSelect(BaseEventData eventData)
    {
        if (eventData is AxisEventData)
        {
            PlayTilt();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && CanPlayPressEffect())
        {
            _isPointerHeld = true;
            PlayScale(_pressedScale, _pressDuration);
        }
    }

    // 떼면 원래 크기로 돌아온다. 패널 위에서 뗐다면 바로 이어서 OnButtonClicked가 확정 연출로 바꾼다.
    // 패널 밖에서 뗐다면 이미 OnPointerExit에서 원래 크기로 돌아와 있다.
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        _isPointerHeld = false;

        if (CanPlayPressEffect())
        {
            PlayScale(1f, _releaseDuration);
        }
    }

    // 클릭(패널 위에서 좌클릭을 뗌) 또는 Enter로 골랐을 때. 원래 크기로 돌아오는 연출이 끝난 뒤에 확정한다.
    private void OnButtonClicked()
    {
        if (_rune == null || _isConfirming || _isInputLocked)
        {
            return;
        }

        if (_visual == null)
        {
            NotifyPicked();
            return;
        }

        _isConfirming = true;
        KillScaleTween();

        if (PickStarted != null)
        {
            PickStarted.Invoke(this);
        }

        Sequence sequence = DOTween.Sequence();

        // 마우스로 눌렀다면 이미 작아져 있다. Enter로 골랐거나 아주 빠르게 클릭해 아직 작아지지 않았다면 작아지는 연출부터 보여 준다.
        if (_visual.localScale.x > (_pressedScale + 1f) * 0.5f)
        {
            sequence.Append(_visual.DOScale(_pressedScale, _pressDuration).SetEase(Ease.OutQuad));
        }

        sequence.Append(_visual.DOScale(1f, _releaseDuration).SetEase(Ease.OutQuad));
        sequence.SetUpdate(true).SetLink(gameObject).OnComplete(ConfirmPick);
        _scaleTween = sequence;
    }

    private void ConfirmPick()
    {
        // 확정을 알리면 창이 닫히며 이 패널이 꺼진다. 그 전에 끝난 트윈 참조와 상태를 비워 둔다.
        _scaleTween = null;
        _isConfirming = false;
        NotifyPicked();
    }

    private void NotifyPicked()
    {
        if (Picked != null)
        {
            Picked.Invoke(_rune);
        }
    }

    private bool CanPlayPressEffect()
    {
        return _visual != null && !_isConfirming && !_isInputLocked && _button != null && _button.interactable;
    }

    private void PlayTilt()
    {
        if (_visual == null || _isConfirming || _isInputLocked)
        {
            return;
        }

        if (_applyTilt == null)
        {
            _applyTilt = ApplyTilt;
        }

        KillTiltTween();
        _tiltTween = DOVirtual.Float(0f, 1f, _tiltDuration, _applyTilt).SetEase(Ease.Linear).SetUpdate(true).SetLink(gameObject);
    }

    // progress: 0~1. 사인 곡선으로 좌우를 _tiltCount번 오간 뒤 0도로 끝난다.
    private void ApplyTilt(float progress)
    {
        float angle = _tiltAngle * Mathf.Sin(progress * _tiltCount * 2f * Mathf.PI);
        _visual.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void PlayScale(float scale, float duration)
    {
        KillScaleTween();
        _scaleTween = _visual.DOScale(scale, duration).SetEase(Ease.OutQuad).SetUpdate(true).SetLink(gameObject);
    }

    private void KillTiltTween()
    {
        if (_tiltTween != null)
        {
            _tiltTween.Kill();
            _tiltTween = null;
        }
    }

    private void KillScaleTween()
    {
        if (_scaleTween != null)
        {
            _scaleTween.Kill();
            _scaleTween = null;
        }
    }

    private string GetCategoryLabel(RuneCategory category)
    {
        switch (category)
        {
            case RuneCategory.Defense:
                return _defenseLabel;
            case RuneCategory.Support:
                return _supportLabel;
            default:
                return _attackLabel;
        }
    }

    private Color GetCategoryColor(RuneCategory category)
    {
        switch (category)
        {
            case RuneCategory.Defense:
                return _defenseColor;
            case RuneCategory.Support:
                return _supportColor;
            default:
                return _attackColor;
        }
    }
}

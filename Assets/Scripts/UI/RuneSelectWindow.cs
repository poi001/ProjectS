using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

// 보스 처치 후 룬 3개 중 1개를 고르는 창 (임시 그레이박스, GDD 6.6). BattleUI 프리팹에 중첩해 둔다.
//  - RuneSelectionRequestedEvent를 받으면 미리 배치한 패널에 룬을 채우고 연다. 룬보다 많은 패널은 숨긴다.
//  - 마우스로 패널을 누르거나, ←/→로 옮기고 Enter로 고른다. (EventSystem의 UI 입력, 첫 패널이 선택된 채로 열린다)
//  - 고르면 창을 닫고 RunePickedEvent를 발행한다. 고르지 않고 닫는 방법은 없다.
//  - 열린 동안 게임을 일시정지한다. (UIWindow의 Pauses Game)
//  - 열릴 때 제목과 패널이 한 덩어리로 화면 중앙에서 작게 시작해 아주 짧은 시간에 원래 크기로 커진다. (DOTween, 어두운 배경은 바로 나온다)
//    일시정지 중에 재생되므로 실제 시간(SetUpdate(true))으로 보간한다. 패널의 기울임·눌림 연출은 RuneChoicePanel이 맡는다.
public class RuneSelectWindow : UIWindow
{
    [Header("패널")]
    [Tooltip("룬을 채울 패널입니다. 최대 선택지 수(3개)만큼 미리 배치해 두고, 왼쪽부터 순서대로 연결합니다.")]
    [SerializeField] private RuneChoicePanel[] _panels = new RuneChoicePanel[0];

    [Header("등장 연출")]
    [Tooltip("창이 열릴 때 화면 중앙에서 작게 시작해 원래 크기로 커지는 오브젝트입니다. 제목과 패널을 담은 Content/Window를 연결합니다. (어두운 배경 Dim은 넣지 않습니다) 비워 두면 연출 없이 바로 나옵니다.")]
    [SerializeField] private RectTransform _popRoot;
    [Tooltip("열리는 순간의 크기 배율입니다. (0 = 보이지 않을 만큼 작음, 1 = 원래 크기)")]
    [SerializeField, Range(0f, 1f)] private float _popStartScale = 0.05f;
    [Tooltip("원래 크기로 커지는 데 걸리는 시간입니다. 실제 시간 기준이라 일시정지 중에도 재생됩니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _popDuration = 0.15f;
    [Tooltip("커지는 속도 곡선입니다. OutBack으로 바꾸면 원래 크기를 살짝 넘었다가 돌아옵니다.")]
    [SerializeField] private Ease _popEase = Ease.OutCubic;

    // 마지막으로 선택되어 있던 패널 버튼. 마우스로 빈 곳을 눌러 선택이 풀리면 여기로 되돌려 키보드 조작이 끊기지 않게 한다.
    private GameObject _lastSelected;
    // 한 패널의 확정 연출이 진행 중인지 여부. 이 동안에는 선택을 그 패널에 묶어 두고 다른 패널은 반응하지 않는다.
    private bool _isPickConfirming;

    private void OnEnable()
    {
        EventBus<RuneSelectionRequestedEvent>.Subscribe(OnRuneSelectionRequested);

        for (int i = 0; i < _panels.Length; i++)
        {
            if (_panels[i] != null)
            {
                _panels[i].PickStarted += OnPanelPickStarted;
                _panels[i].Picked += OnPanelPicked;
            }
        }
    }

    protected override void OnDisable()
    {
        EventBus<RuneSelectionRequestedEvent>.Unsubscribe(OnRuneSelectionRequested);

        for (int i = 0; i < _panels.Length; i++)
        {
            if (_panels[i] != null)
            {
                _panels[i].PickStarted -= OnPanelPickStarted;
                _panels[i].Picked -= OnPanelPicked;
            }
        }

        base.OnDisable();
    }

    private void Update()
    {
        if (!IsOpen)
        {
            return;
        }

        EventSystem eventSystem = EventSystem.current;

        if (eventSystem == null)
        {
            return;
        }

        GameObject selected = eventSystem.currentSelectedGameObject;

        // 확정 연출 중에는 키보드로 선택을 옮겨도 고른 패널로 되돌린다.
        if (_isPickConfirming)
        {
            if (selected != _lastSelected && _lastSelected != null)
            {
                eventSystem.SetSelectedGameObject(_lastSelected);
            }

            return;
        }

        if (IsPanelButton(selected))
        {
            _lastSelected = selected;
        }
        else if (_lastSelected != null)
        {
            eventSystem.SetSelectedGameObject(_lastSelected);
        }
    }

    // 룬을 패널에 채우고 창을 연다. 이미 열려 있으면 무시한다.
    public void Show(IReadOnlyList<RuneDataSO> runes)
    {
        if (IsOpen || !HasAnyRune(runes))
        {
            if (!IsOpen)
            {
                Debug.LogWarning("[RuneSelectWindow] 보여 줄 룬이 없어 룬 선택 창을 열지 않습니다.", this);
            }

            return;
        }

        GameObject firstButton = null;

        for (int i = 0; i < _panels.Length; i++)
        {
            RuneChoicePanel panel = _panels[i];

            if (panel == null)
            {
                continue;
            }

            bool hasRune = i < runes.Count && runes[i] != null;
            panel.gameObject.SetActive(hasRune);

            if (!hasRune)
            {
                continue;
            }

            panel.Setup(runes[i]);

            if (firstButton == null && panel.Button != null)
            {
                firstButton = panel.Button.gameObject;
            }
        }

        Open();
        _lastSelected = firstButton;

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(firstButton);
        }
    }

    protected override void OnOpened()
    {
        if (_popRoot == null)
        {
            return;
        }

        _popRoot.DOKill();
        _popRoot.localScale = Vector3.one * _popStartScale;
        _popRoot.DOScale(1f, _popDuration).SetEase(_popEase).SetUpdate(true).SetLink(gameObject);
    }

    protected override void OnClosed()
    {
        _lastSelected = null;
        _isPickConfirming = false;

        if (_popRoot != null)
        {
            _popRoot.DOKill();
            _popRoot.localScale = Vector3.one;
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    private void OnRuneSelectionRequested(RuneSelectionRequestedEvent requestedEvent)
    {
        Show(requestedEvent.Runes);
    }

    // 한 패널의 확정 연출이 시작되면 모든 패널의 입력을 잠가, 연출이 끝나기 전에 다른 패널을 눌러도 반응하지 않게 한다.
    // 잠금은 창이 닫혀 패널이 꺼질 때(RuneChoicePanel.ResetEffects) 풀린다.
    private void OnPanelPickStarted(RuneChoicePanel pickedPanel)
    {
        if (!IsOpen)
        {
            return;
        }

        _isPickConfirming = true;

        for (int i = 0; i < _panels.Length; i++)
        {
            if (_panels[i] != null)
            {
                _panels[i].SetInputLocked(true);
            }
        }

        if (pickedPanel.Button != null)
        {
            _lastSelected = pickedPanel.Button.gameObject;

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(_lastSelected);
            }
        }
    }

    private void OnPanelPicked(RuneDataSO rune)
    {
        if (!IsOpen)
        {
            return;
        }

        // 일시정지를 먼저 풀고 알린다. (받는 쪽이 평소 상태에서 처리하도록)
        Close();
        EventBus<RunePickedEvent>.Publish(new RunePickedEvent(rune));
    }

    private bool HasAnyRune(IReadOnlyList<RuneDataSO> runes)
    {
        if (runes == null)
        {
            return false;
        }

        int count = Mathf.Min(runes.Count, _panels.Length);

        for (int i = 0; i < count; i++)
        {
            if (runes[i] != null && _panels[i] != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsPanelButton(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        for (int i = 0; i < _panels.Length; i++)
        {
            RuneChoicePanel panel = _panels[i];

            if (panel != null && panel.gameObject.activeInHierarchy && panel.Button != null && panel.Button.gameObject == target)
            {
                return true;
            }
        }

        return false;
    }
}

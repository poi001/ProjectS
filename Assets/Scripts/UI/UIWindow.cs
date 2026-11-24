using UnityEngine;

// 열고 닫는 UI 창의 공통 부모. (사운드 옵션 창, 룬 선택 창 등) 체력바 같은 HUD는 상속하지 않는다.
// 루트 오브젝트는 항상 켜 두고(입력·이벤트를 받아야 하므로), 자식 Content(화면을 덮는 Dim + 창)만 켜고 끈다.
// 열린 동안 게임을 멈추도록 고를 수 있다. (GameManager.SetPaused) 닫히거나 꺼질 때(씬 전환 등) 반드시 해제한다.
// 일시정지는 겹쳐 쌓이지 않으므로, 열 때 이미 다른 쪽이 멈춰 두었다면 이 창은 일시정지를 건드리지 않는다. (닫을 때 남의 일시정지를 풀지 않도록)
// 자식 클래스는 Awake/OnDisable을 오버라이드할 때 base를 호출하고, 열고 닫힐 때 할 일은 OnOpened/OnClosed에 덧붙인다.
public abstract class UIWindow : MonoBehaviour
{
    [Header("창")]
    [Tooltip("창을 열 때 켜고 닫을 때 끄는 오브젝트입니다. 화면을 덮는 어두운 배경(Dim)과 창 전체를 담은 Content를 연결합니다.")]
    [SerializeField] private GameObject _content;
    [Tooltip("창이 열린 동안 게임을 일시정지할지 여부입니다. 켜면 게임 시간이 멈추고 플레이어 입력이 잠깁니다.")]
    [SerializeField] private bool _pausesGame = true;

    // 이 창이 열면서 직접 켠 일시정지인지 여부. 직접 켠 경우에만 닫을 때 해제한다.
    private bool _ownsPause;

    public bool IsOpen { get; private set; }

    protected virtual void Awake()
    {
        if (_content != null)
        {
            _content.SetActive(false);
        }
    }

    // 창이 열린 채로 꺼지면(씬 전환 등) 일시정지가 남지 않도록 닫는다.
    protected virtual void OnDisable()
    {
        Close();
    }

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;

        if (_content != null)
        {
            _content.SetActive(true);
        }

        _ownsPause = _pausesGame && GameManager.HasInstance && !GameManager.Instance.IsPaused;

        if (_ownsPause)
        {
            GameManager.Instance.SetPaused(true);
        }

        OnOpened();
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;

        if (_content != null)
        {
            _content.SetActive(false);
        }

        if (_ownsPause && GameManager.HasInstance)
        {
            GameManager.Instance.SetPaused(false);
        }

        _ownsPause = false;
        OnClosed();
    }

    // Content가 켜지고 일시정지가 적용된 뒤에 호출된다.
    protected virtual void OnOpened()
    {
    }

    // Content가 꺼지고 일시정지가 해제된 뒤에 호출된다.
    protected virtual void OnClosed()
    {
    }
}

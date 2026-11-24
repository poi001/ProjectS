using UnityEngine;

// 전투 중의 룬 하나. 액티브 룬(ActiveRune)과 패시브 룬(PassiveRune)의 공통 부모다. (GDD 5.2)
// 룬의 정의(이름·아이콘·수치)는 에셋(RuneDataSO)에 있고 런타임에 바꾸지 않는다.
// 전투 중에 달라지는 값(타수 세기, 버프의 남은 시간 등)과 효과 로직은 이 클래스를 상속한 쪽이 가진다.
// 인스턴스는 정의 데이터의 CreateRune(ActiveRuneDataSO / PassiveRuneDataSO)으로 만든다.
//
// 수명: 룬을 가진 쪽이 Init(주인)으로 시작하고 Release로 끝낸다. (캐릭터의 Init / Release와 같은 방식)
//  - Init은 주인(Player)의 Init이 모두 끝난 뒤(OnInitCompleted 이후)에 부른다.
//    그 전에는 스탯이 아직 없거나, 스탯이 바뀌어도 Player가 받지 못해(Changed 구독 전) 최대 체력·이동속도 같은 보정이 반영되지 않는다.
//  - Release는 룬을 뗄 때, 그리고 주인이 Release되기 전에 반드시 부른다. Init에서 한 일(이벤트 구독, 스탯 보정 등)을 여기서 되돌린다.
//  - 주인이 다시 Init하면 스탯이 새로 만들어지므로 룬도 Release한 뒤 다시 Init한다. (룬을 Init한 채로 주인만 다시 Init하면 효과가 사라진다)
//  - 기다림이 필요한 효과(몇 초 동안 강화 등)는 UniTask를 주인의 destroyCancellationToken과 연결하고 OnRelease에서 취소한다. (Coding_Rules "비동기")
public abstract class Rune
{
    private bool _isInitialized;

    // 이 룬의 정의 데이터. 이름·아이콘·설명·갈래는 여기서 읽는다.
    public RuneDataSO Data { get; }
    // 이 룬을 장착한 플레이어. Init부터 Release까지만 들어 있다.
    public Player Owner { get; private set; }
    public bool IsInitialized => _isInitialized;

    protected Rune(RuneDataSO data)
    {
        Data = data;
    }

    public void Init(Player owner)
    {
        if (_isInitialized)
        {
            Debug.LogWarning($"[Rune] 이미 시작한 룬을 다시 Init했습니다. 무시합니다. ({GetType().Name})");
            return;
        }

        if (owner == null)
        {
            Debug.LogWarning($"[Rune] 주인(Player)이 없어 룬을 시작할 수 없습니다. ({GetType().Name})");
            return;
        }

        Owner = owner;
        _isInitialized = true;
        OnInit();
    }

    // 시작하지 않았거나 이미 끝낸 룬에 불러도 안전하다.
    public void Release()
    {
        if (!_isInitialized)
        {
            return;
        }

        // 씬이 내려가며 주인이 먼저 파괴된 경우에도 구독 해제 등은 해야 하므로, 주인의 생존 여부와 관계없이 부른다.
        // 정리 도중 예외가 나도 끝낸 상태로 남겨, 다시 Release했을 때 같은 정리가 반복되지 않게 한다.
        try
        {
            OnRelease();
        }
        finally
        {
            _isInitialized = false;
            Owner = null;
        }
    }

    // 룬을 시작할 때 할 일. Owner가 들어 있다.
    protected virtual void OnInit()
    {
    }

    // 룬을 끝낼 때 할 일. OnInit에서 한 일을 되돌린다. 아직 Owner가 들어 있지만 이미 파괴된 오브젝트일 수 있다.
    protected virtual void OnRelease()
    {
    }
}

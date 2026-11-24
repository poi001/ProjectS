// 전투 중의 패시브 룬. 장착해 두면 전투에 들어가자마자 발동해 전투 내내 적용된다. (GDD 5.2)
// 룬 종류(스탯 보정, 3타마다 추가 피해, 대시 충돌 피해 등)마다 이 클래스를 상속한다.
//  - OnActivated: 발동할 때 한 번. 효과를 건다. (스탯 보정 걸기, EventBus나 Owner의 이벤트 구독 등)
//  - OnDeactivated: 해제할 때 한 번. OnActivated에서 한 일을 모두 되돌린다. (보정 걷기, 구독 해제)
//    OnActivated가 도중에 실패했더라도 불릴 수 있으므로, 걸지 못한 것을 걷어도 문제가 없게 쓴다.
// 발동과 해제는 룬의 수명(Init / Release)과 같다. 재사용 대기시간이나 발동 키는 없다.
public abstract class PassiveRune : Rune
{
    public new PassiveRuneDataSO Data { get; }

    protected PassiveRune(PassiveRuneDataSO data)
        : base(data)
    {
        Data = data;
    }

    protected sealed override void OnInit()
    {
        OnActivated();
    }

    protected sealed override void OnRelease()
    {
        OnDeactivated();
    }

    // 발동할 때 한 번 호출된다. Owner가 들어 있다.
    protected abstract void OnActivated();

    // 해제할 때 한 번 호출된다. 아직 Owner가 들어 있지만 이미 파괴된 오브젝트일 수 있다.
    protected abstract void OnDeactivated();
}

// 플레이어가 결석에 닿아 주웠을 때 EventBus로 발행하는 이벤트. RewardStone이 사라지기 직전에 발행한다.
// 구독자: BattleResultManager(결석의 종류에 따라 동작 해금·룬 선택 창·액티브 룬 지급을 처리한다)
// 결석이 무엇을 주는지는 결석이 아니라 보스 데이터(EnemyStatsSO)에 있으므로 여기에는 종류만 담는다.
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct RewardStoneCollectedEvent
{
    // 주운 결석의 종류 (리워드 / 액티브 룬 / 패시브 룬)
    public RewardType Type;

    public RewardStoneCollectedEvent(RewardType type)
    {
        Type = type;
    }
}

using UnityEngine;

// 플레이어가 결석에 닿아 주웠을 때 EventBus로 발행하는 이벤트. RewardStone이 사라지기 직전에 발행한다.
// 구독자: RewardAcquiredWindow(획득 창을 연다)
// 실제 획득 처리(동작 해금, 룬 지급)를 하는 쪽은 아직 없다. 만들 때 이 이벤트를 구독하고, 지급할 내용(어떤 동작·어떤 룬인지)을 여기에 추가한다.
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct RewardStoneCollectedEvent
{
    // 보상의 종류 (기본 동작 / 액티브 룬 / 패시브 룬)
    public RewardType Type;
    // 획득 창에 보여 줄 아이콘. 없으면 null이다.
    public Sprite Icon;
    // 획득 창에 보여 줄 설명
    public string Description;

    public RewardStoneCollectedEvent(RewardType type, Sprite icon, string description)
    {
        Type = type;
        Icon = icon;
        Description = description;
    }
}

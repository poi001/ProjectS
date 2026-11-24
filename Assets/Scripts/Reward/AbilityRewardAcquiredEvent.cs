using UnityEngine;

// 리워드 결석을 주워 기본 동작을 얻었을 때 EventBus로 발행하는 이벤트. BattleResultManager가 동작을 해금한 뒤에 발행한다.
// 구독자: RewardAcquiredWindow(획득 창을 연다)
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct AbilityRewardAcquiredEvent
{
    // 해금된 동작. 여러 개일 수 있다. (예: WallSlide | WallJump)
    public PlayerAbility Ability;
    // 획득 창에 보여 줄 아이콘. 없으면 null이다.
    public Sprite Icon;
    // 획득 창에 보여 줄 설명
    public string Description;

    public AbilityRewardAcquiredEvent(PlayerAbility ability, Sprite icon, string description)
    {
        Ability = ability;
        Icon = icon;
        Description = description;
    }
}

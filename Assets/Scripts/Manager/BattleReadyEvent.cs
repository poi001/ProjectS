// 전투 준비(캐릭터 소환과 Init, 체력바 연결)가 끝났을 때 BattleSceneInitialManager가 EventBus로 발행하는 이벤트.
// 로딩 화면 닫기, 전투 시작 연출처럼 "준비가 끝난 뒤" 해야 하는 시스템이 구독한다.
// 구독이 발행보다 늦으면 받지 못하므로, 그런 경우에는 BattleSceneInitialManager.IsReady로 확인한다.
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct BattleReadyEvent
{
    // 소환된 플레이어
    public Player Player;
    // 소환된 보스
    public Enemy Boss;

    public BattleReadyEvent(Player player, Enemy boss)
    {
        Player = player;
        Boss = boss;
    }
}

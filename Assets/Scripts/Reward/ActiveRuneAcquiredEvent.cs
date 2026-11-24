// 액티브 룬 결석을 주워 액티브 룬을 얻었을 때 EventBus로 발행하는 이벤트. BattleResultManager가 발행한다. (창은 뜨지 않는다, GDD 6.6)
// 구독자: 아직 없음. 룬 시스템(장착·보관)을 만들 때 이 이벤트를 받아 얻은 룬을 저장한다.
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct ActiveRuneAcquiredEvent
{
    // 얻은 액티브 룬. 보스 데이터에 룬이 비어 있었다면 null이다.
    public ActiveRuneDataSO Rune;

    public ActiveRuneAcquiredEvent(ActiveRuneDataSO rune)
    {
        Rune = rune;
    }
}

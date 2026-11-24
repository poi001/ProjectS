// 룬 선택 창에서 룬을 골랐을 때 EventBus로 발행하는 이벤트. RuneSelectWindow가 창을 닫은 뒤 발행한다.
// 룬 선택 창에는 패시브 룬만 나오므로(GDD 5.4) 고른 룬도 패시브 룬이다.
// 구독자: 아직 없음. 룬 시스템(장착·보관)을 만들 때 이 이벤트를 받아 고른 룬을 저장한다.
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct RunePickedEvent
{
    // 고른 패시브 룬
    public PassiveRuneDataSO Rune;

    public RunePickedEvent(PassiveRuneDataSO rune)
    {
        Rune = rune;
    }
}

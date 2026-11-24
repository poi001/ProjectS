using System.Collections.Generic;

// 룬 선택 창을 열어 달라고 EventBus로 발행하는 이벤트. BattleResultManager가 보스 처치 후 발행한다.
// 구독자: RuneSelectWindow(창을 열고 패널에 룬을 채운다)
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct RuneSelectionRequestedEvent
{
    // 선택지로 보여 줄 룬 (보통 3개)
    public IReadOnlyList<RuneDataSO> Runes;

    public RuneSelectionRequestedEvent(IReadOnlyList<RuneDataSO> runes)
    {
        Runes = runes;
    }
}

// 게임이 일시정지되거나 다시 진행될 때 EventBus로 발행하는 이벤트. GameManager.SetPaused가 발행한다.
// 구독자: Player(입력 잠금)
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct GamePauseChangedEvent
{
    // true = 일시정지됨, false = 다시 진행됨
    public bool IsPaused;

    public GamePauseChangedEvent(bool isPaused)
    {
        IsPaused = isPaused;
    }
}

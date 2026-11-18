// Mushroom 상태 공통 베이스. 상태기계는 Enemy 타입으로 공유하므로,
// Mushroom 전용 멤버(패턴 상태, 패턴 수치)에 접근할 수 있도록 Mushroom 타입 참조를 따로 보관한다.
public abstract class MushroomState : EnemyState
{
    protected readonly Mushroom mushroom;

    // 지금도 이 상태가 진행 중인지 여부. 몸 판정으로 플레이어가 죽으면 그 자리에서 Idle로 바뀌므로,
    // 판정을 낸 뒤 남은 처리를 하기 전에 확인한다. (이미 끝난 상태가 이동이나 다음 패턴 전환을 이어 가지 않게 한다)
    protected bool IsCurrentState => stateMachine.CurrentState == this;

    protected MushroomState(Mushroom owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
        mushroom = owner;
    }
}

// Knight 상태 공통 베이스. 상태기계는 Enemy 타입으로 공유하므로,
// Knight 전용 멤버(패턴 상태, 패턴 수치, KnightAnimation)에 접근할 수 있도록 Knight 타입 참조를 따로 보관한다.
public abstract class KnightState : EnemyState
{
    protected readonly Knight knight;

    protected KnightState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
        knight = owner;
    }
}

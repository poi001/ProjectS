// Mossback 상태 공통 베이스. 상태기계는 Enemy 타입으로 공유하므로,
// Mossback 전용 멤버(패턴 상태, 패턴 수치)에 접근할 수 있도록 Mossback 타입 참조를 따로 보관한다.
public abstract class MossbackState : EnemyState
{
    protected readonly Mossback mossback;

    protected MossbackState(Mossback owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
        mossback = owner;
    }
}

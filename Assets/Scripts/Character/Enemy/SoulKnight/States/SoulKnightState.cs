// SoulKnight 상태 공통 베이스. 상태기계는 Enemy 타입으로 공유하므로,
// SoulKnight 전용 멤버(패턴 상태, 패턴 수치)에 접근할 수 있도록 SoulKnight 타입 참조를 따로 보관한다.
public abstract class SoulKnightState : EnemyState
{
    protected readonly SoulKnight boss;

    protected SoulKnightState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
        boss = owner;
    }
}

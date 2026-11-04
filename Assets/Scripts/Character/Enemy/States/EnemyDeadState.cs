// 사망 상태. 사망 애니메이션을 재생하고 멈춘다.
// 보스 처치 후 처리(룬 보상 등)는 기획이 정해지면 구현한다.
public class EnemyDeadState : EnemyState
{
    public EnemyDeadState(Enemy owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.Movement.SetVelocityX(0f);
        owner.Animation.Play(EnemyAnimation.Dead);
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.SetVelocityX(0f);
    }
}

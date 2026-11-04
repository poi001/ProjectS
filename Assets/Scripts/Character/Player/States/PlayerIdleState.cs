// 땅에서 가만히 있는 상태
public class PlayerIdleState : PlayerState
{
    public PlayerIdleState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.Animation.Play(PlayerAnimation.Idle);
    }

    public override void LogicUpdate()
    {
        if (TryChangeFromGroundedState())
        {
            return;
        }

        if (IsMoving())
        {
            stateMachine.ChangeState(owner.RunState);
        }
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(0f);
    }
}

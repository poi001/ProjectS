// 땅에서 좌우로 이동하는 상태
public class PlayerRunState : PlayerState
{
    public PlayerRunState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.Animation.Play(PlayerAnimation.Run);
    }

    public override void LogicUpdate()
    {
        UpdateFacing();

        if (TryChangeFromGroundedState())
        {
            return;
        }

        if (!IsMoving())
        {
            stateMachine.ChangeState(owner.IdleState);
        }
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(GetMoveInput());
    }
}

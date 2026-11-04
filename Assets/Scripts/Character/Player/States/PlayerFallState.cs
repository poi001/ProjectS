// 공중에 있는 상태. 착지하면 Idle/Run으로 전환한다.
public class PlayerFallState : PlayerState
{
    public PlayerFallState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.Animation.Play(PlayerAnimation.FallLoop);
    }

    public override void LogicUpdate()
    {
        UpdateFacing();

        // 발판에서 떨어진 직후라면 코요테 타임 안에서 점프할 수 있다.
        if (owner.CanJump)
        {
            stateMachine.ChangeState(owner.JumpState);
            return;
        }

        if (owner.Movement.IsGrounded)
        {
            ChangeToGroundedState();
            return;
        }

        TryChangeToJumpAttackState();
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(GetMoveInput());
    }
}

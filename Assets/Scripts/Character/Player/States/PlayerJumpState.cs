// 점프 상태. Jump 애니메이션이 끝나면 접지 여부에 따라 Fall 또는 Idle/Run으로 전환한다.
public class PlayerJumpState : PlayerState
{
    public PlayerJumpState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.InputHandler.ConsumeJumpBuffer();
        owner.Movement.Jump();
        owner.Animation.Play(PlayerAnimation.Jump);
    }

    public override void LogicUpdate()
    {
        UpdateFacing();

        if (TryChangeToJumpAttackState())
        {
            return;
        }

        if (!owner.Animation.IsFinished(PlayerAnimation.Jump))
        {
            return;
        }

        if (owner.Movement.IsGrounded)
        {
            ChangeToGroundedState();
        }
        else
        {
            stateMachine.ChangeState(owner.FallState);
        }
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(GetMoveInput());
    }
}

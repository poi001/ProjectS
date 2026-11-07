// 점프 상태. Jump 애니메이션이 끝나면 접지 여부에 따라 Fall 또는 Idle/Run으로 전환한다.
// 점프 중에는 대시, 공중 점프, 점프 공격, 벽타기로 전환할 수 있다.
// 공중 점프(PlayerAirJumpState)는 이 상태를 상속해 뛰어오르는 힘만 바꾼다.
public class PlayerJumpState : PlayerState
{
    public PlayerJumpState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.InputHandler.ConsumeJumpBuffer();
        ApplyJumpVelocity();
        owner.Animation.Play(PlayerAnimation.Jump);
    }

    public override void LogicUpdate()
    {
        UpdateFacing();

        if (TryChangeFromAirborneState())
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

    // 땅에서 하는 점프는 기본 점프 높이로 뛰어오른다.
    protected virtual void ApplyJumpVelocity()
    {
        owner.Movement.Jump();
    }
}

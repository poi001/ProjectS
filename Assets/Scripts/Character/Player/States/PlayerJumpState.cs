// 점프 상태. 뛰어오르는 동작(JumpStart)을 재생하고, 끝나면 접지 여부에 따라 Fall 또는 땅 위 상태로 전환한다.
// 점프는 JumpStart(이 상태, 뛰어오르는 동작) → Fall(PlayerFallState, 공중에 있는 동안)로 이어진다. 착지 동작은 따로 없다.
// 점프 중에는 대시, 공중 점프, 공중 공격, 벽타기로 전환할 수 있다.
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
        owner.Animation.Play(PlayerAnimation.JumpStart);
    }

    public override void LogicUpdate()
    {
        UpdateFacing();

        if (TryChangeFromAirborneState())
        {
            return;
        }

        if (!owner.Animation.IsFinished(PlayerAnimation.JumpStart))
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

// 공중에 있는 상태. 공중 자세(Fall)를 유지하다가 땅에 닿으면 Idle/Run(입력이 있으면 그 동작)으로 전환한다.
// 점프의 뛰어오르는 동작(JumpStart)이 끝난 뒤, 발판에서 떨어졌을 때, 벽 점프·대시·공중 공격이 끝난 뒤에 이 상태가 된다.
// 떨어지는 중에는 대시, 공중 점프, 공중 공격, 벽타기로 전환할 수 있다.
public class PlayerFallState : PlayerState
{
    public PlayerFallState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.Animation.Play(PlayerAnimation.Fall);
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

        TryChangeFromAirborneState();
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(GetMoveInput());
    }
}

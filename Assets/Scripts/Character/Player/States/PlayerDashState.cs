using UnityEngine;

// 대시 상태. 정해진 시간 동안 한 방향으로 빠르게 수평 이동한다. (중력 없음)
// 방향은 상태에 들어가기 전에 SetDirection으로 정하고, 그 방향을 바라본다. (PlayerState.TryChangeToDashState가 이동 입력 또는 바라보는 방향으로 정한다)
// 대시하는 동안에는 피격 판정 콜라이더를 꺼서 적의 공격 판정 조회 자체에 걸리지 않는다.
// 공중에서 시작하면 공중 대시를 소모하고, 끝나면 대시 쿨타임이 시작된다.
// 대시 중에는 다른 입력을 받지 않으며, 끝나면 Fall 또는 Idle/Run(점프·공격 입력이 있으면 그 동작)으로 전환한다.
public class PlayerDashState : PlayerState
{
    // 대시 방향 (오른쪽 1, 왼쪽 -1)
    private float _direction = 1f;
    private float _endTime;

    public PlayerDashState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 다음 Enter에서 대시할 방향을 정한다. (오른쪽 양수, 왼쪽 음수)
    public void SetDirection(float direction)
    {
        _direction = direction >= 0f ? 1f : -1f;
    }

    public override void Enter()
    {
        owner.InputHandler.ConsumeDashBuffer();
        owner.ConsumeDash();
        owner.Animation.UpdateFacing(_direction);
        owner.Movement.SetGravityEnabled(false);
        owner.Movement.SetVelocity(GetDashVelocity());
        owner.SetHurtboxEnabled(false);
        owner.Animation.Play(PlayerAnimation.Dash);
        _endTime = Time.time + owner.DashDuration;
    }

    public override void Exit()
    {
        owner.Movement.SetGravityEnabled(true);
        owner.SetHurtboxEnabled(true);
        owner.StartDashCooldown();
    }

    public override void LogicUpdate()
    {
        if (Time.time < _endTime)
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
        owner.Movement.SetVelocity(GetDashVelocity());
    }

    private Vector2 GetDashVelocity()
    {
        return new Vector2(_direction * owner.DashSpeed, 0f);
    }
}

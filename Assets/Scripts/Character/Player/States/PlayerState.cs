using UnityEngine;

// 플레이어 상태 공통 베이스. 여러 상태가 함께 쓰는 입력 조회와 전환 규칙을 모아둔다.
public abstract class PlayerState : State<Player>
{
    private const float MoveThreshold = 0.01f;

    protected PlayerState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected float GetMoveInput()
    {
        return owner.InputHandler.MoveInput;
    }

    protected bool IsMoving()
    {
        return Mathf.Abs(GetMoveInput()) > MoveThreshold;
    }

    protected void UpdateFacing()
    {
        owner.Animation.UpdateFacing(GetMoveInput());
    }

    // 땅 위 상태로 돌아갈 때 점프 > 대시 > 공격 > 이동 여부(Run/Idle) 순으로 다음 상태를 고른다.
    // 착지/공격 종료 프레임에 들어온 입력도 놓치지 않기 위함이다.
    // attackBufferExtraTime: 애니메이션 종료를 늦게 감지한 만큼 공격 버퍼 시간을 늘려서 확인한다.
    protected void ChangeToGroundedState(float attackBufferExtraTime = 0f)
    {
        if (owner.CanJump)
        {
            stateMachine.ChangeState(owner.JumpState);
            return;
        }

        if (TryChangeToDashState())
        {
            return;
        }

        if (owner.InputHandler.IsAttackBuffered(attackBufferExtraTime))
        {
            stateMachine.ChangeState(owner.AttackState);
            return;
        }

        stateMachine.ChangeState(IsMoving() ? owner.RunState : owner.IdleState);
    }

    // 공중 상태(Jump/Fall/WallJump) 공통 전환. 대시 > 공중 점프 > 점프 공격 > 벽타기 순으로 확인한다. 전환했다면 true를 반환한다.
    protected bool TryChangeFromAirborneState()
    {
        return TryChangeToDashState()
            || TryChangeToAirJumpState()
            || TryChangeToJumpAttackState()
            || TryChangeToWallSlideState();
    }

    // 대시 입력이 있고 대시할 수 있으면 이동 입력 방향으로, 입력이 없으면 바라보는 방향으로 대시한다. 전환했다면 true를 반환한다.
    protected bool TryChangeToDashState()
    {
        float direction = IsMoving() ? Mathf.Sign(GetMoveInput()) : owner.Animation.FacingDirection;
        return TryChangeToDashState(direction);
    }

    // 방향을 직접 정해 대시한다. (벽타기 중에는 벽 반대쪽으로) 전환했다면 true를 반환한다.
    protected bool TryChangeToDashState(float direction)
    {
        if (!owner.CanDash)
        {
            return false;
        }

        owner.DashState.SetDirection(direction);
        stateMachine.ChangeState(owner.DashState);
        return true;
    }

    // 공중에서 점프 입력이 있고 공중 점프 횟수가 남아 있으면 공중 점프한다. 전환했다면 true를 반환한다.
    protected bool TryChangeToAirJumpState()
    {
        if (!owner.CanAirJump)
        {
            return false;
        }

        owner.ConsumeAirJump();
        stateMachine.ChangeState(owner.AirJumpState);
        return true;
    }

    // 공중에서 벽 쪽으로 이동 키를 누르고 있고 그쪽 벽에 닿아 있으면 벽타기로 전환한다. 전환했다면 true를 반환한다.
    // 올라가는 중에는 붙지 않는다. (벽 옆에서 뛰어오르는 점프가 끊기지 않게 하고, 벽 점프 직후 같은 벽에 다시 붙지 않게 한다)
    protected bool TryChangeToWallSlideState()
    {
        if (owner.Movement.IsGrounded || !IsMoving() || owner.Movement.Velocity.y > 0f)
        {
            return false;
        }

        float direction = Mathf.Sign(GetMoveInput());

        if (!owner.Movement.IsTouchingWall(direction))
        {
            return false;
        }

        owner.WallSlideState.SetWallDirection(direction);
        stateMachine.ChangeState(owner.WallSlideState);
        return true;
    }

    // 공중 상태(Jump/Fall)에서 공격 입력이 버퍼에 있으면 점프 공격으로 전환한다. 전환했다면 true를 반환한다.
    protected bool TryChangeToJumpAttackState()
    {
        if (owner.Movement.IsGrounded || !owner.InputHandler.HasBufferedAttack)
        {
            return false;
        }

        stateMachine.ChangeState(owner.JumpAttackState);
        return true;
    }

    // 공격 애니메이션이 끝나기 직전(버퍼 시간 이내)에 공격 입력이 있었는지 확인한다.
    // 애니메이션 종료를 늦게 감지한 만큼 보정해서, 실제 종료 시각 기준으로 버퍼 시간을 적용한다.
    protected bool HasBufferedAttackBeforeFinish(int animationHash)
    {
        float timeSinceFinished = owner.Animation.GetTimeSinceFinished(animationHash);
        return owner.InputHandler.IsAttackBuffered(timeSinceFinished);
    }

    // Idle/Run 공통 전환(점프, 대시, 낙하, 공격). 전환했다면 true를 반환한다.
    protected bool TryChangeFromGroundedState()
    {
        if (owner.CanJump)
        {
            stateMachine.ChangeState(owner.JumpState);
            return true;
        }

        if (TryChangeToDashState())
        {
            return true;
        }

        if (!owner.Movement.IsGrounded)
        {
            stateMachine.ChangeState(owner.FallState);
            return true;
        }

        if (owner.InputHandler.HasBufferedAttack)
        {
            stateMachine.ChangeState(owner.AttackState);
            return true;
        }

        return false;
    }
}

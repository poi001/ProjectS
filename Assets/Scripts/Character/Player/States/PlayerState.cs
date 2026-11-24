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
        return owner.FilterMoveInput(owner.InputHandler.MoveInput);
    }

    protected bool IsMoving()
    {
        return Mathf.Abs(GetMoveInput()) > MoveThreshold;
    }

    protected void UpdateFacing()
    {
        owner.Animation.UpdateFacing(GetMoveInput());
    }

    // 땅 위 상태로 돌아갈 때 점프 > 대시 > 공격 > 스킬 > 블로킹 > 체력 회복 > 이동 여부(Run/Idle) 순으로 다음 상태를 고른다.
    // 착지/공격 종료 프레임에 들어온 입력도 놓치지 않기 위함이다.
    // attackBufferExtraTime: 애니메이션 종료를 늦게 감지한 만큼 공격 버퍼 시간을 늘려서 확인한다.
    protected void ChangeToGroundedState(float attackBufferExtraTime = 0f)
    {
        if (TryChangeToGroundedAction(attackBufferExtraTime))
        {
            return;
        }

        stateMachine.ChangeState(IsMoving() ? owner.RunState : owner.IdleState);
    }

    // 땅 위에서 입력으로 시작하는 동작(점프 > 대시 > 공격 > 스킬 > 블로킹 > 체력 회복)으로 전환한다. 전환했다면 true를 반환한다.
    private bool TryChangeToGroundedAction(float attackBufferExtraTime)
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

        if (TryChangeToAttackState(attackBufferExtraTime))
        {
            return true;
        }

        return TryChangeToSkillState() || TryChangeToBlockState() || TryChangeToHealState();
    }

    // 땅 위에서 동작의 끝부분(공격 복귀 동작 등)을 입력으로 끊을 때 쓴다. 점프 > 대시 > 공격 > 스킬 > 블로킹 > 체력 회복 > 이동(Run) 순으로 확인한다.
    // 입력이 없으면 전환하지 않고 false를 반환한다. (발밑이 사라져도 낙하로 넘어가지 않는다. 동작이 끝난 뒤에 처리한다)
    protected bool TryInterruptOnGround()
    {
        if (TryChangeToGroundedAction(0f))
        {
            return true;
        }

        if (IsMoving())
        {
            stateMachine.ChangeState(owner.RunState);
            return true;
        }

        return false;
    }

    // 스킬 입력이 있고 스킬을 쓸 수 있으면(땅 위, 쿨타임 종료) 스킬을 쓴다. 전환했다면 true를 반환한다.
    protected bool TryChangeToSkillState()
    {
        if (!owner.CanSkill)
        {
            return false;
        }

        stateMachine.ChangeState(owner.SkillState);
        return true;
    }

    // 블로킹 입력이 있고 블로킹할 수 있으면(땅 위, 쿨타임 종료) 블로킹한다. 전환했다면 true를 반환한다.
    protected bool TryChangeToBlockState()
    {
        if (!owner.CanBlock)
        {
            return false;
        }

        stateMachine.ChangeState(owner.BlockState);
        return true;
    }

    // 체력 회복 입력(위 방향키를 새로 누르고 계속 누르는 중)이 있고 회복할 수 있으면(땅 위) 체력 회복을 시작한다. 전환했다면 true를 반환한다.
    protected bool TryChangeToHealState()
    {
        if (!owner.CanHeal)
        {
            return false;
        }

        stateMachine.ChangeState(owner.HealState);
        return true;
    }

    // 공중 상태(Jump/Fall/WallJump) 공통 전환. 대시 > 공중 점프 > 공중 공격 > 벽타기 순으로 확인한다. 전환했다면 true를 반환한다.
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
    // 마지막으로 벽 점프한 벽에는 붙지 않는다. (같은 벽에서 벽 점프를 반복해 계속 오르지 못하게 한다)
    // 벽타기를 얻지 못했으면(Knight 처치) 벽에 닿아도 붙지 않는다.
    protected bool TryChangeToWallSlideState()
    {
        if (!owner.HasAbility(PlayerAbility.WallSlide))
        {
            return false;
        }

        if (owner.Movement.IsGrounded || !IsMoving() || owner.Movement.Velocity.y > 0f)
        {
            return false;
        }

        float direction = Mathf.Sign(GetMoveInput());
        Collider2D wall = owner.Movement.GetTouchingWall(direction);

        if (wall == null || owner.IsLastWallJumpedWall(wall))
        {
            return false;
        }

        owner.WallSlideState.SetWallDirection(direction);
        stateMachine.ChangeState(owner.WallSlideState);
        return true;
    }

    // 공중 상태(Jump/Fall)에서 공격 입력이 버퍼에 있으면 공중 공격으로 전환한다. 전환했다면 true를 반환한다.
    protected bool TryChangeToJumpAttackState()
    {
        if (!owner.CanAirAttack || !owner.InputHandler.HasBufferedAttack)
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

    // Idle/Run 공통 전환(점프, 대시, 낙하, 공격, 스킬, 블로킹, 체력 회복). 전환했다면 true를 반환한다.
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

        if (TryChangeToAttackState(0f))
        {
            return true;
        }

        return TryChangeToSkillState() || TryChangeToBlockState() || TryChangeToHealState();
    }

    // 땅 위 공격 입력이 있으면 공격 상태로 전환한다. 전환했다면 true를 반환한다.
    // 시네마틱이 공격 간격을 정해 둔 동안(Mushroom 반응속도 기믹)에는 간격이 지나기 전에 누른 입력을 버리고 false를 반환한다.
    // 입력을 버리지 않으면 간격이 끝나는 순간 그동안 누른 연타가 한꺼번에 나가므로, 누른 즉시 버려 간격 뒤에 새로 눌러야 하게 한다.
    // attackBufferExtraTime: 애니메이션 종료를 늦게 감지한 만큼 공격 버퍼 시간을 늘려서 확인한다.
    private bool TryChangeToAttackState(float attackBufferExtraTime)
    {
        if (!owner.InputHandler.IsAttackBuffered(attackBufferExtraTime))
        {
            return false;
        }

        if (owner.IsAttackOnCooldown)
        {
            owner.InputHandler.ConsumeAttackBuffer();
            return false;
        }

        stateMachine.ChangeState(owner.AttackState);
        return true;
    }
}

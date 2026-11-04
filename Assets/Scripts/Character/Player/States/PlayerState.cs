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

    // 땅 위 상태로 돌아갈 때 점프 > 공격 > 이동 여부(Run/Idle) 순으로 다음 상태를 고른다.
    // 착지/공격 종료 프레임에 들어온 입력도 놓치지 않기 위함이다.
    // attackBufferExtraTime: 애니메이션 종료를 늦게 감지한 만큼 공격 버퍼 시간을 늘려서 확인한다.
    protected void ChangeToGroundedState(float attackBufferExtraTime = 0f)
    {
        if (owner.CanJump)
        {
            stateMachine.ChangeState(owner.JumpState);
            return;
        }

        if (owner.InputHandler.IsAttackBuffered(attackBufferExtraTime))
        {
            stateMachine.ChangeState(owner.AttackState);
            return;
        }

        stateMachine.ChangeState(IsMoving() ? owner.RunState : owner.IdleState);
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

    // Idle/Run 공통 전환(점프, 낙하, 공격). 전환했다면 true를 반환한다.
    protected bool TryChangeFromGroundedState()
    {
        if (owner.CanJump)
        {
            stateMachine.ChangeState(owner.JumpState);
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

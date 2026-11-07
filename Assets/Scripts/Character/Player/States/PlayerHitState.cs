using UnityEngine;

// 피격 상태. 진행 중이던 동작을 끊고 피해를 입은 방향의 반대로 튕겨 나가며 Hit 애니메이션을 재생한다.
// 튕겨 나가는 방향은 상태에 들어가기 전에 SetKnockbackDirection으로 정한다. (Player가 피해를 준 쪽을 보고 정한다)
// Hit 애니메이션이 끝날 때까지 이동·점프·공격 입력을 받지 않는다. (끝나는 즉시 조작 가능)
// 무적 시간은 Player가 피격 시점에 시작하므로, 이 상태가 끝난 뒤에도 이어질 수 있다.
public class PlayerHitState : PlayerState
{
    // 튕겨 나가는 방향 (오른쪽 1, 왼쪽 -1)
    private float _knockbackDirection = 1f;

    public PlayerHitState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 다음 Enter에서 튕겨 나갈 방향을 정한다. (오른쪽 양수, 왼쪽 음수)
    public void SetKnockbackDirection(float direction)
    {
        _knockbackDirection = direction >= 0f ? 1f : -1f;
    }

    public override void Enter()
    {
        // 맞기 전에 눌러 둔 입력이 피격 직후에 실행되지 않도록 비운다.
        owner.InputHandler.ConsumeJumpBuffer();
        owner.InputHandler.ConsumeAttackBuffer();

        Vector2 knockbackVelocity = owner.KnockbackVelocity;
        owner.Movement.Launch(new Vector2(_knockbackDirection * knockbackVelocity.x, knockbackVelocity.y));
        owner.Animation.Play(PlayerAnimation.Hit);
    }

    public override void LogicUpdate()
    {
        if (!owner.Animation.IsFinished(PlayerAnimation.Hit))
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
        // 공중에서는 넉백 속도를 그대로 유지하고, 착지하면 미끄러지지 않도록 멈춘다.
        // Launch가 잠시 접지 판정을 막아 두므로, 접지 상태라면 실제로 착지한 것이다.
        if (owner.Movement.IsGrounded)
        {
            owner.Movement.SetVelocityX(0f);
        }
    }
}

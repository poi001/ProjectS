using System;

// 공중 공격 상태. 모션은 Combo1과 같지만 별도의 JumpAttack, JumpAttack_End 애니메이션 상태를 쓴다.
// JumpAttack이 끝나면 JumpAttack_End(후딜레이)를 재생하고, 끝난 뒤 땅이면 Idle/Run으로 돌아간다.
// 공중이면 공격이 끝나기 직전(버퍼 시간 이내)에 공격 입력이 있었다면 다시 공격하고, 없다면 Fall로 돌아간다.
// 공중에서는 이동할 수 있고, 땅에 닿으면 멈춘다. 방향은 공격 중 바뀌지 않는다.
// 데미지 판정은 클립의 애니메이션 이벤트(타격 프레임)마다 점프 공격 판정과 콤보 1의 계수로 한 번씩 한다.
public class PlayerJumpAttackState : PlayerState
{
    private bool _isEnding;
    // 상태에 진입할 때마다 델리게이트가 새로 할당되지 않도록 한 번만 만들어 둔다.
    private readonly Action _attackHitFrameHandler;

    public PlayerJumpAttackState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
        _attackHitFrameHandler = OnAttackHitFrame;
    }

    public override void Enter()
    {
        _isEnding = false;
        owner.Animation.AttackHitFrame += _attackHitFrameHandler;
        owner.InputHandler.ConsumeAttackBuffer();
        owner.Animation.Play(PlayerAnimation.JumpAttack);
    }

    public override void Exit()
    {
        owner.Animation.AttackHitFrame -= _attackHitFrameHandler;
    }

    public override void LogicUpdate()
    {
        if (!_isEnding)
        {
            UpdateAttack();
            return;
        }

        if (!owner.Animation.IsFinished(PlayerAnimation.JumpAttackEnd))
        {
            return;
        }

        // 애니메이션 종료를 늦게 감지한 만큼 보정해서, 실제 종료 시각 기준으로 공격 버퍼 시간을 적용한다.
        float timeSinceFinished = owner.Animation.GetTimeSinceFinished(PlayerAnimation.JumpAttackEnd);

        // 공격 중 착지했더라도 종료 애니메이션까지 끝까지 재생한 뒤, 땅 위 상태 규칙(점프 > 공격 > Run/Idle)에 따라 전환한다.
        if (owner.Movement.IsGrounded)
        {
            ChangeToGroundedState(timeSinceFinished);
        }
        else if (owner.InputHandler.IsAttackBuffered(timeSinceFinished))
        {
            stateMachine.ChangeState(this);
        }
        else
        {
            stateMachine.ChangeState(owner.FallState);
        }
    }

    public override void PhysicsUpdate()
    {
        float moveInput = owner.Movement.IsGrounded ? 0f : GetMoveInput();
        owner.Movement.Move(moveInput);
    }

    private void UpdateAttack()
    {
        if (!owner.Animation.IsFinished(PlayerAnimation.JumpAttack))
        {
            return;
        }

        _isEnding = true;
        owner.Animation.Play(PlayerAnimation.JumpAttackEnd);
    }

    private void OnAttackHitFrame()
    {
        owner.HitChecker.ResetHitTargets();
        owner.HitChecker.Hit(owner.JumpAttackHitbox, owner.JumpAttackDamage, owner.Animation.FacingDirection);
    }
}

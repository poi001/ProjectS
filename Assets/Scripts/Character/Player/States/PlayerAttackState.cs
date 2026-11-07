using System;

// 지상 기본 공격 상태. 콤보 1 → 2 → ... 순으로 이어지며, 이어가지 않으면 해당 콤보의 종료 애니메이션을 재생한 뒤 Idle/Run으로 돌아간다.
// 콤보 수는 Player.ComboCount를 따른다. 공격 중에는 멈춰 있고 방향도 바뀌지 않는다.
// 데미지 판정은 콤보 클립의 애니메이션 이벤트(타격 프레임)마다 현재 콤보의 판정과 계수로 한 번씩 한다.
public class PlayerAttackState : PlayerState
{
    private int _comboIndex;
    private bool _isEnding;
    // 상태에 진입할 때마다 델리게이트가 새로 할당되지 않도록 한 번만 만들어 둔다.
    private readonly Action _attackHitFrameHandler;

    private int CurrentComboHash => owner.Animation.GetComboHash(_comboIndex);
    private int CurrentComboEndHash => owner.Animation.GetComboEndHash(_comboIndex);

    public PlayerAttackState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
        _attackHitFrameHandler = OnAttackHitFrame;
    }

    public override void Enter()
    {
        _comboIndex = 0;
        _isEnding = false;
        owner.Animation.AttackHitFrame += _attackHitFrameHandler;
        owner.InputHandler.ConsumeAttackBuffer();
        owner.Movement.Move(0f);
        owner.Animation.Play(CurrentComboHash);
    }

    public override void Exit()
    {
        owner.Animation.AttackHitFrame -= _attackHitFrameHandler;
    }

    public override void LogicUpdate()
    {
        if (!_isEnding)
        {
            UpdateCombo();
            return;
        }

        if (!owner.Animation.IsFinished(CurrentComboEndHash))
        {
            return;
        }

        // 공격 중 발밑이 사라졌더라도 진행 중이던 콤보를 종료 애니메이션까지 모두 마친 뒤에 낙하한다.
        if (owner.Movement.IsGrounded)
        {
            ChangeToGroundedState(owner.Animation.GetTimeSinceFinished(CurrentComboEndHash));
        }
        else
        {
            stateMachine.ChangeState(owner.FallState);
        }
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(0f);
    }

    private void UpdateCombo()
    {
        if (!owner.Animation.IsFinished(CurrentComboHash))
        {
            return;
        }

        if (CanContinueCombo())
        {
            _comboIndex++;
            owner.InputHandler.ConsumeAttackBuffer();
            owner.Animation.Play(CurrentComboHash);
            return;
        }

        _isEnding = true;
        owner.Animation.Play(CurrentComboEndHash);
    }

    // 다음 콤보가 남아 있고, 땅 위에 있으며, 공격 애니메이션이 끝나기 직전(버퍼 시간 이내)에 공격 입력이 있었다면 콤보를 이어간다.
    private bool CanContinueCombo()
    {
        bool hasNextCombo = _comboIndex < owner.ComboCount - 1;

        if (!hasNextCombo || !owner.Movement.IsGrounded)
        {
            return false;
        }

        return HasBufferedAttackBeforeFinish(CurrentComboHash);
    }

    // 타격 이벤트마다 새 공격으로 보고 판정한다. (한 클립에 이벤트가 여러 개면 여러 번 때린다.)
    private void OnAttackHitFrame()
    {
        PlayerAttack attack = owner.GetComboAttack(_comboIndex);

        if (attack == null)
        {
            return;
        }

        owner.HitChecker.ResetHitTargets();
        owner.HitChecker.Hit(attack.Hitbox, owner.GetDamage(attack), owner.Animation.FacingDirection);
    }
}

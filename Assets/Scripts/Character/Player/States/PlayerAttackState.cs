using System;

// 지상 기본 공격 상태. 콤보 1 → 2 → ... 순으로 이어지며, 이어가지 않으면 그 타의 복귀 동작(Combo{n}_End)을 재생한 뒤 Idle/Run으로 돌아간다.
// 복귀 동작은 입력으로 끊을 수 있다. 재생 중에 점프·대시·공격·스킬·블로킹·체력 회복·이동 입력이 들어오면 바로 그 동작으로 넘어간다. (PlayerState.TryInterruptOnGround)
// 때리는 동작(Combo{n}) 자체는 끊기지 않는다.
// 콤보 수는 Player.ComboCount를 따른다. 공격 중에는 멈춰 있고 방향도 바뀌지 않는다.
// 시네마틱이 공격 간격을 정해 둔 동안에는 공격을 시작한 뒤 그 간격이 지나기 전까지 새 공격을 시작하지 못한다. (Player.IsAttackOnCooldown, PlayerState가 확인한다)
// 데미지 판정은 콤보 클립의 애니메이션 이벤트(타격 프레임)마다 현재 콤보의 판정과 계수로 한 번씩 한다.
public class PlayerAttackState : PlayerState
{
    private int _comboIndex;
    // 복귀 동작(Combo{n}_End)을 재생하는 중인지 여부
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
        // 시네마틱이 공격 간격을 정해 두었다면(Mushroom 반응속도 기믹) 다음 공격을 시작할 수 있는 시각을 정한다.
        owner.NotifyAttackStarted();
    }

    public override void Exit()
    {
        owner.Animation.AttackHitFrame -= _attackHitFrameHandler;
    }

    public override void LogicUpdate()
    {
        if (_isEnding)
        {
            // 복귀 동작은 땅 위에서 들어온 입력으로 끊는다.
            if (owner.Movement.IsGrounded && TryInterruptOnGround())
            {
                return;
            }

            if (owner.Animation.IsFinished(CurrentComboEndHash))
            {
                Finish(CurrentComboEndHash);
            }

            return;
        }

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

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(0f);
    }

    // 공격을 끝내고 다음 상태로 넘어간다. finishedAnimationHash: 방금 끝난 애니메이션 (공격 버퍼 시간 보정용)
    // 공격 중 발밑이 사라졌더라도 진행 중이던 동작을 끝까지 마친 뒤에 낙하한다.
    private void Finish(int finishedAnimationHash)
    {
        if (owner.Movement.IsGrounded)
        {
            // 끝난 직후에 누른 공격은 1타부터 다시 시작한다. (애니메이션 종료를 늦게 감지한 만큼 버퍼 시간을 보정한다)
            ChangeToGroundedState(owner.Animation.GetTimeSinceFinished(finishedAnimationHash));
        }
        else
        {
            stateMachine.ChangeState(owner.FallState);
        }
    }

    // 다음 콤보가 남아 있고, 땅 위에 있으며, 공격 애니메이션이 끝나기 직전(버퍼 시간 이내)에 공격 입력이 있었다면 콤보를 이어간다.
    // 시네마틱이 콤보를 막아 둔 동안(Player.IsComboBlocked)에는 이어가지 않고 그 타로 끝낸다.
    private bool CanContinueCombo()
    {
        bool hasNextCombo = _comboIndex < owner.ComboCount - 1;

        if (!hasNextCombo || !owner.Movement.IsGrounded || owner.IsComboBlocked)
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

        // 치명타와 피해 흡수는 Player가 타격마다 처리한다.
        owner.PerformAttackHit(attack);
    }
}

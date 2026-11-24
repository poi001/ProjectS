using System;

// 공중 공격 상태. 공중 콤보 1 → 2 → ... (JumpAttack{n}) 순으로 이어지며, 이어가지 않으면 그 타로 끝내고 Fall로 돌아간다.
// 공격하는 동안에도 중력을 받아 떨어지고, 공중에서는 좌우로 움직일 수 있다. 땅에 닿으면 멈춘다. 방향은 공격 중 바뀌지 않는다.
// 횟수 제한은 없다. 공격이 끝나기 직전(버퍼 시간 이내)에 누른 공격이 다음 타로 이어지지 못했다면(마지막 타였다면) 1타부터 다시 공격한다.
// 이어가지 않으면 그 타의 복귀 동작(JumpAttack{n}_End)을 재생한다. 재생 중에 입력이 들어오면 바로 넘어간다. (때리는 동작 JumpAttack{n} 자체는 끊기지 않는다)
// 공중이면 대시·공중 점프·공중 공격·벽타기, 땅이면 점프·대시·공격·스킬·블로킹·체력 회복·이동. 입력이 없으면 끝까지 재생한 뒤 끝낸다.
// 콤보 수는 Player.AirComboCount를 따른다. (지금 2타)
// 데미지 판정은 클립의 애니메이션 이벤트(타격 프레임)마다 현재 공중 콤보의 판정과 계수로 한 번씩 한다.
public class PlayerJumpAttackState : PlayerState
{
    private int _comboIndex;
    // 복귀 동작(JumpAttack{n}_End)을 재생하는 중인지 여부
    private bool _isEnding;
    // 상태에 진입할 때마다 델리게이트가 새로 할당되지 않도록 한 번만 만들어 둔다.
    private readonly Action _attackHitFrameHandler;

    private int CurrentComboHash => owner.Animation.GetAirComboHash(_comboIndex);
    private int CurrentComboEndHash => owner.Animation.GetAirComboEndHash(_comboIndex);

    public PlayerJumpAttackState(Player owner, StateMachine<Player> stateMachine)
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
        owner.Animation.Play(CurrentComboHash);
    }

    public override void Exit()
    {
        owner.Animation.AttackHitFrame -= _attackHitFrameHandler;
    }

    public override void LogicUpdate()
    {
        if (_isEnding)
        {
            // 복귀 동작은 입력으로 끊는다. 공중이면 공중 동작, 땅이면 땅 위 동작으로 넘어간다.
            bool isInterrupted = owner.Movement.IsGrounded ? TryInterruptOnGround() : TryChangeFromAirborneState();

            if (isInterrupted)
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
        float moveInput = owner.Movement.IsGrounded ? 0f : GetMoveInput();
        owner.Movement.Move(moveInput);
    }

    // 공격을 끝내고 다음 상태로 넘어간다. finishedAnimationHash: 방금 끝난 애니메이션 (공격 버퍼 시간 보정용)
    private void Finish(int finishedAnimationHash)
    {
        // 애니메이션 종료를 늦게 감지한 만큼 보정해서, 실제 종료 시각 기준으로 공격 버퍼 시간을 적용한다.
        float timeSinceFinished = owner.Animation.GetTimeSinceFinished(finishedAnimationHash);

        // 공격 중 착지했더라도 동작을 끝까지 재생한 뒤, 땅 위 상태 규칙(점프 > 대시 > 공격 > 스킬 > 블로킹 > 체력 회복 > Run/Idle)에 따라 전환한다.
        if (owner.Movement.IsGrounded)
        {
            ChangeToGroundedState(timeSinceFinished);
        }
        else if (owner.InputHandler.IsAttackBuffered(timeSinceFinished))
        {
            // 아직 공중이고 공격 입력이 남아 있으면 1타부터 다시 공격한다. (Fall을 거치면 공중 자세가 한 프레임 끼어든다)
            stateMachine.ChangeState(this);
        }
        else
        {
            stateMachine.ChangeState(owner.FallState);
        }
    }

    // 다음 공중 콤보가 남아 있고, 공격 애니메이션이 끝나기 직전(버퍼 시간 이내)에 공격 입력이 있었다면 콤보를 이어간다.
    // 시네마틱이 콤보를 막아 둔 동안(Player.IsComboBlocked)에는 이어가지 않고 그 타로 끝낸다.
    private bool CanContinueCombo()
    {
        bool hasNextCombo = _comboIndex < owner.AirComboCount - 1;

        if (!hasNextCombo || owner.IsComboBlocked)
        {
            return false;
        }

        return HasBufferedAttackBeforeFinish(CurrentComboHash);
    }

    // 타격 이벤트마다 새 공격으로 보고 판정한다. (한 클립에 이벤트가 여러 개면 여러 번 때린다.)
    private void OnAttackHitFrame()
    {
        PlayerAttack attack = owner.GetAirComboAttack(_comboIndex);

        if (attack == null)
        {
            return;
        }

        // 치명타와 피해 흡수는 Player가 타격마다 처리한다.
        owner.PerformAttackHit(attack);
    }
}

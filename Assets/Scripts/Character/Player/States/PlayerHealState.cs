// 체력 회복 상태. 땅 위에서 회복 키(위 방향키)를 누르고 있으면 회복 동작(Heal, 1초)을 재생하고, 끝까지 마치면 체력을 회복한다.
// 회복은 동작이 끝난 순간 한 번 일어난다. (Player의 Heal Amount) 다음 경우에는 회복하지 않고 끊긴다.
//  - 피해를 입으면: Player가 피격 상태로 바꾸면서 이 상태가 끝난다. (끝까지 마치지 못하므로 회복도 없다)
//  - 키를 중간에 떼면: 땅 위 상태로 돌아간다. (일시정지·시네마틱으로 입력이 잠긴 동안에는 떼었는지 확인하지 않고 동작을 유지한다)
// 동작 중에는 이동·점프·공격·대시 입력을 받지 않고 멈춰 있으며, 방향도 바뀌지 않는다.
// 끝나면 Idle/Run(입력이 있으면 그 동작)으로 돌아간다. 키를 계속 누르고 있어도 다시 시작하지 않는다. (새로 눌러야 한다)
public class PlayerHealState : PlayerState
{
    public PlayerHealState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.InputHandler.ConsumeHealBuffer();
        owner.Movement.Move(0f);
        owner.Animation.Play(PlayerAnimation.Heal);
    }

    public override void LogicUpdate()
    {
        // 키를 떼면 회복하지 않고 끊는다. 입력이 잠긴 동안은 키 상태를 읽을 수 없으므로 확인하지 않는다.
        if (!owner.InputHandler.IsLocked && !owner.InputHandler.IsHealHeld)
        {
            ExitToNextState(0f);
            return;
        }

        if (!owner.Animation.IsFinished(PlayerAnimation.Heal))
        {
            return;
        }

        owner.CompleteHeal();
        ExitToNextState(owner.Animation.GetTimeSinceFinished(PlayerAnimation.Heal));
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(0f);
    }

    // 회복 중 발밑이 사라졌다면 낙하한다. 아니면 땅 위 상태로 돌아간다.
    private void ExitToNextState(float attackBufferExtraTime)
    {
        if (owner.Movement.IsGrounded)
        {
            ChangeToGroundedState(attackBufferExtraTime);
        }
        else
        {
            stateMachine.ChangeState(owner.FallState);
        }
    }
}

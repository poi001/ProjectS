// 회복(Health). 2페이즈에서 Idle 뒤 패턴으로 선택되면 들어온다. (Enemy_GDD 6.12)
// 제자리에 멈춰 Health 애니메이션(0.8초)을 한 번 재생하고, 애니메이션이 끝나는 순간 체력을 회복(HealAmount)한 뒤 Idle로 간다.
// 회복 시점은 애니메이션 길이가 정한다. 클립을 고치면 회복까지 걸리는 시간도 함께 바뀐다.
// 모션 도중 피해를 입으면 회복이 끊긴다. 맞는 순간 모션을 멈추고 바로 Idle로 가며 체력은 오르지 않는다. (플레이어가 때려서 회복을 막는다)
// 끊는 처리는 SoulKnight의 피격 처리(OnDamaged)가 맡는다. 이 상태는 끝까지 재생된 경우만 회복한다.
public class SoulKnightHealthState : SoulKnightState
{
    public SoulKnightHealthState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        boss.Movement.SetVelocityX(0f);
        boss.Animation.Play(SoulKnightAnimation.Health);
    }

    public override void LogicUpdate()
    {
        if (!boss.Animation.IsFinished(SoulKnightAnimation.Health))
        {
            return;
        }

        boss.Vital.Heal(boss.HealAmount);
        boss.ChangeToRest();
    }

    public override void PhysicsUpdate()
    {
        boss.Movement.SetVelocityX(0f);
    }
}

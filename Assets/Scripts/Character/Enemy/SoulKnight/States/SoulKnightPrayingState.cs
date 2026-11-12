// 기도. 1페이즈에서 Idle 중 체력이 최대 체력의 50% 이하이면 각성하면서 한 번 들어온다. (Enemy_GDD 6.8)
// 그 자리에 멈춰 Praying 애니메이션(5초)을 한 번 재생하고, 애니메이션이 끝나면 항상 Idle로 간다. 기도 시간은 애니메이션 길이가 정한다.
// 애니메이션은 0~1.5초에 Pray_0~9, 1.5~4.5초에 Pray_9를 유지, 4.5~5초에 Pray_10·11이다. 클립을 고치면 기도 시간도 함께 바뀐다.
// 기도하는 동안에는 피해 무효다. 플레이어의 공격은 맞은 것으로 처리되어 히트 이펙트와 0 데미지 숫자, 전용 피격음이 나오지만 체력은 줄지 않는다.
// 피해를 입지 않으므로 패턴 게이지가 꽉 차 있어도 기절하지 않으며, 그동안 기절 가능 표시도 끈다.
public class SoulKnightPrayingState : SoulKnightState
{
    public SoulKnightPrayingState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        boss.Vital.SetDamageNullified(true);
        boss.Movement.SetVelocityX(0f);
        boss.Animation.Play(SoulKnightAnimation.Praying);
        boss.RefreshStunReadyEffect();
    }

    // 플레이어 사망 등으로 중간에 끊겨도 피해 무효를 끈다.
    public override void Exit()
    {
        boss.Vital.SetDamageNullified(false);
    }

    public override void LogicUpdate()
    {
        if (!boss.Animation.IsFinished(SoulKnightAnimation.Praying))
        {
            return;
        }

        boss.ChangeToRest();
        // 기도가 끝났으므로 게이지가 꽉 차 있다면 기절 가능 표시를 다시 켠다. (상태가 바뀐 뒤에 확인한다)
        boss.RefreshStunReadyEffect();
    }

    public override void PhysicsUpdate()
    {
        boss.Movement.SetVelocityX(0f);
    }
}

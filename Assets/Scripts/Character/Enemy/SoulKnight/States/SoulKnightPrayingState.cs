using UnityEngine;

// 기도. 1페이즈에서 Idle 중 체력이 최대 체력의 50% 이하이면 전투당 한 번 들어온다. (Enemy_GDD 6.8)
// 그 자리에 멈춰 정해진 시간(PrayingDuration) 동안 기도 자세로 있고, 끝나면 Idle로 쉰 뒤 기존 패턴을 이어간다.
// 기도하는 동안에는 피해 무효다. 플레이어의 공격은 맞은 것으로 처리되어 히트 이펙트와 0 데미지 숫자, 전용 피격음이 나오지만 체력은 줄지 않는다.
// 피해를 입지 않으므로 패턴 게이지가 꽉 차 있어도 기절하지 않으며, 그동안 기절 가능 표시도 끈다.
// Praying 클립은 반복하지 않으므로 기도 시간이 더 길면 마지막 프레임(기도 자세)을 유지한다.
public class SoulKnightPrayingState : SoulKnightState
{
    private float _enterTime;

    public SoulKnightPrayingState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
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
        if (Time.time - _enterTime < boss.PrayingDuration)
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

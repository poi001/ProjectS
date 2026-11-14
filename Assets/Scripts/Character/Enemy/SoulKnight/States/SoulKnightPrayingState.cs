using UnityEngine;

// 기도. 들어올 때의 페이즈(Enemy.CurrentPhase)에 따라 다르게 동작한다.
// - 1페이즈 (각성 표시, Enemy_GDD 6.8): Idle 중 체력이 최대 체력의 50% 이하이면 각성하면서 한 번 들어온다.
//   Praying 애니메이션(5초)을 한 번 재생하고, 애니메이션이 끝나면 Idle로 간다. 기도 시간은 애니메이션 길이가 정한다.
//   애니메이션은 0~1.5초에 Pray_0~9, 1.5~4.5초에 Pray_9를 유지, 4.5~5초에 Pray_10·11이다. 클립을 고치면 기도 시간도 함께 바뀐다.
//   기도가 끝날 때(완전히 일어선 모습이 되는 AwakenAuraStartProgress 지점) 각성 오라를 시작한다. 오라는 1페이즈 다운까지 남는다.
// - 2페이즈 (기믹, Enemy_GDD 6.11): 시간으로 찬 패턴 게이지가 꽉 찬 채로 Idle이 되면 들어오며, 몇 번이든 반복한다.
//   기도하는 동안 게이지가 차던 속도 그대로 빠지고, 0이 되면 Idle로 간다. 기도 시간은 게이지가 정한다. (1초에 5%면 20초)
//   같은 Praying 애니메이션을 쓰되, 기도 자세(Phase2PrayingHoldProgress 직전)에서 멈춰 두다가 게이지가 다 빠지기 직전에 일어나는 모습을 재생한다.
//   기도하는 동안 기믹이 진행된다. 보스는 Ghost 레이어가 되어 바닥 말고는 아무것과도 충돌하지 않고(플레이어의 공격도 통과),
//   맵에 기둥이 솟아오르며 그중 하나의 꼭대기에 목표물이 놓인다. 플레이어가 목표물을 공격하면 게이지를 비우고 바로 Idle로 간다.
//   못 치고 게이지가 다 빠져도 기도는 끝난다. 나갈 때 기둥이 내려간다. (SoulKnight.BeginPrayingGimmick / EndPrayingGimmick)
// 두 페이즈 모두 그 자리에 멈춰 있고, 기도하는 동안에는 피해 무효다. 플레이어의 공격은 맞은 것으로 처리되어
// 히트 이펙트와 0 데미지 숫자, 전용 피격음이 나오지만 체력은 줄지 않는다.
// 피해를 입지 않으므로 패턴 게이지가 꽉 차 있어도 기절하지 않으며, 그동안 기절 가능 표시도 끈다.
// 기도하는 동안 발밑에 보라색 룬(pixelFX arcane/rune-circle, 4초 반복)이 돈다. 시작·끝에 0.2초 페이드하며, Feel 피드백(SoulKnight의 기도 연출)이 담당한다.
public class SoulKnightPrayingState : SoulKnightState
{
    // 게이지가 빠지는 동안 유지하는 기도인지. 들어올 때의 페이즈로 정한다. (2페이즈부터 true)
    private bool _isGaugeDrained;
    // 2페이즈 기도에서 멈춰 두기를 풀고 일어나는 모습을 재생하기 시작했는지. 한 번 풀면 다시 멈추지 않는다.
    private bool _isStandingUp;

    public SoulKnightPrayingState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _isGaugeDrained = boss.CurrentPhase >= 2;
        _isStandingUp = false;

        boss.Vital.SetDamageNullified(true);
        boss.Movement.SetVelocityX(0f);
        boss.Animation.Play(SoulKnightAnimation.Praying);
        boss.RefreshStunReadyEffect();
        boss.PlayPrayingRune();

        if (_isGaugeDrained)
        {
            boss.BeginPrayingGimmick();
        }
    }

    // 플레이어 사망 등으로 중간에 끊겨도 피해 무효를 끄고 발밑 룬을 지운다.
    // 1페이즈 기도는 이미 각성한 뒤이므로, 오라가 시작되기 전에 끊겼더라도 나갈 때 오라를 켠다. (이미 켜져 있으면 아무 일도 없다)
    public override void Exit()
    {
        boss.Vital.SetDamageNullified(false);
        boss.StopPrayingRune();

        if (!_isGaugeDrained)
        {
            boss.PlayAwakenAura();
        }
        else
        {
            boss.EndPrayingGimmick();
        }
    }

    public override void LogicUpdate()
    {
        if (_isGaugeDrained)
        {
            UpdateGaugeDrain();
            return;
        }

        if (boss.Animation.WillReach(SoulKnightAnimation.Praying, boss.AwakenAuraStartProgress))
        {
            boss.PlayAwakenAura();
        }

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

    // 2페이즈 기도: 게이지를 차던 속도로 빼고, 다 빠지면 Idle로 간다.
    private void UpdateGaugeDrain()
    {
        // 플레이어가 기둥 위 목표물을 공격했으면 기믹이 끝난다. 남은 게이지를 비우고 바로 Idle로 간다.
        if (boss.IsPrayingGimmickCleared)
        {
            boss.PatternGauge.SetValue(0f);
            boss.ChangeToRest();
            return;
        }

        float drainPerSecond = boss.Phase2GaugeFillPerSecond;

        // 속도가 0이면 게이지가 영영 빠지지 않으므로 바로 비우고 끝낸다.
        if (drainPerSecond <= 0f)
        {
            boss.PatternGauge.SetValue(0f);
        }
        else
        {
            boss.PatternGauge.Reduce(drainPerSecond * Time.deltaTime);
        }

        if (boss.PatternGauge.CurrentValue <= 0f)
        {
            boss.ChangeToRest();
            return;
        }

        if (_isStandingUp)
        {
            return;
        }

        // 일어나는 모습(멈춰 두는 지점부터 클립 끝까지)에 걸리는 시간. 그 시간보다 게이지가 많이 남아 있는 동안에는 기도 자세에서 멈춰 둔다.
        // 남은 시간이 그보다 짧아지면 멈추기를 풀어, 일어나는 모습이 게이지가 다 빠지는 때에 맞춰 끝난다.
        int praying = SoulKnightAnimation.Praying;
        float holdProgress = boss.Phase2PrayingHoldProgress;
        float standUpTime = boss.Animation.GetRemainingTime(praying) - boss.Animation.GetTimeUntil(praying, holdProgress);
        float remainingDrainTime = boss.PatternGauge.CurrentValue / drainPerSecond;

        if (remainingDrainTime > standUpTime)
        {
            boss.Animation.HoldBefore(praying, holdProgress);
            return;
        }

        _isStandingUp = true;
    }
}

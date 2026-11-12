using UnityEngine;

// 기절. 패턴 게이지가 꽉 찬 상태에서 플레이어에게 피해를 입으면 진행 중인 패턴을 끊고 들어온다.
// 정해진 시간(StunDuration) 동안 제자리에 멈춰 있고, 그동안 게이지가 서서히 줄어 기절이 끝날 때 0이 된다.
// 기절이 끝나면 항상 일어나기(KnightStunRecoverState)로 가고, 일어나기가 끝나면 Idle로 간다.
// 전용 스프라이트가 없어 Death 클립을 쓴다. 클립이 반복하지 않으므로 기절 시간이 더 길면 마지막 프레임(쓰러진 모습)을 유지한다.
// 기절하는 동안 머리 위에 기절 VFX(Knight.StunVfxPrefab)를 띄운다.
public class KnightStunState : KnightState
{
    private float _enterTime;
    // 기절을 시작할 때의 게이지 값. 여기서부터 0까지 줄인다.
    private float _startGaugeValue;

    public KnightStunState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        knight.RefreshStunReadyEffect();
        knight.PlayStunSound();
        _enterTime = Time.time;
        _startGaugeValue = knight.PatternGauge.CurrentValue;
        knight.Movement.SetVelocityX(0f);
        knight.Animation.Play(KnightAnimation.Stun);
        knight.ShowStatusVfx(knight.StunVfxPrefab);
    }

    // 기절이 중간에 끊기는 경우(플레이어 사망, Knight 사망 등)에도 게이지를 비우고 VFX를 끈다.
    public override void Exit()
    {
        knight.PatternGauge.SetValue(0f);
        knight.HideStatusVfx(knight.StunVfxPrefab);
    }

    public override void LogicUpdate()
    {
        float duration = knight.StunDuration;
        float progress = duration > 0f ? (Time.time - _enterTime) / duration : 1f;

        if (progress >= 1f)
        {
            // 일어나기 동안에는 게이지가 0인 상태를 유지한다.
            knight.PatternGauge.SetValue(0f);
            stateMachine.ChangeState(knight.StunRecoverState);
            return;
        }

        knight.PatternGauge.SetValue(_startGaugeValue * (1f - progress));
    }

    public override void PhysicsUpdate()
    {
        knight.Movement.SetVelocityX(0f);
    }
}

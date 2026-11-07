using UnityEngine;

// 기절. 패턴 게이지가 꽉 찬 채로 패턴이 끝나면 Idle·구르기·공격 대신 들어온다. (공격 중에 차도 그 공격은 끝까지 한다.)
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
            // 일어나기가 끝나 Idle로 넘어갈 때 꽉 찬 게이지로 다시 기절하지 않도록 여기서 비운다.
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

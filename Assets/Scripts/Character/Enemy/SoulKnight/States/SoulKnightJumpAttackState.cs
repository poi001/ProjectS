using UnityEngine;

// 점프공격. 점프의 최고점에서 자신의 바로 아래로 수직으로 내려찍는다. (Enemy_GDD 6장)
// 낙하 중에는 중력을 끄고 정해진 속도로 곧장 떨어진다. 낙하 중 몸에 닿아도 피해는 없다.
// 바닥에 닿는(접지) 순간 그 자리에서 SoulKnight 주변에 범위 충격을 한 번 낸다. (다단히트 없음) 충격음과 카메라 흔들림도 함께 낸다.
// 충격 뒤에는 착지 모습을 재생하고 Idle로 넘어간다.
// 내려찍기를 시작할 때 패턴 게이지가 차고(Knight의 슬라이딩과 같은 양), 충격이 플레이어를 맞히면 준다.
// 바닥에 닿지 못하는 예외 상황은 MaxFallDuration이 지나면 그 자리에서 충격을 내는 것으로 막는다.
// 애니메이션은 JumpAttack 클립 하나를 나눠 쓴다. 착지 지점(JumpAttackLandProgress) 직전의 낙하 모습에서 멈춘 채 떨어지고, 바닥에 닿으면 나머지를 재생한다.
public class SoulKnightJumpAttackState : SoulKnightState
{
    // 바닥에 닿지 못하는 예외 상황에서 낙하를 끝내는 시간 (단위: 초)
    private const float MaxFallDuration = 2f;

    private float _enterTime;
    private bool _isLanding;

    public SoulKnightJumpAttackState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        boss.PatternGauge.Add(boss.PatternGaugeGain);
        _enterTime = Time.time;
        _isLanding = false;

        boss.Movement.SetGravityEnabled(false);
        boss.Movement.SetVelocity(new Vector2(0f, -boss.JumpAttackFallSpeed));
        boss.Animation.Play(SoulKnightAnimation.JumpAttack);
    }

    public override void Exit()
    {
        boss.Movement.SetGravityEnabled(true);
        boss.Movement.SetVelocity(Vector2.zero);
    }

    public override void LogicUpdate()
    {
        if (_isLanding)
        {
            if (boss.Animation.WillReach(SoulKnightAnimation.JumpAttack, boss.JumpAttackEndProgress))
            {
                boss.ChangeToRest();
            }

            return;
        }

        // 떨어지는 동안에는 착지 모습이 나오지 않도록 낙하 모습에서 멈춰 둔다.
        boss.Animation.HoldBefore(SoulKnightAnimation.JumpAttack, boss.JumpAttackLandProgress);
    }

    public override void PhysicsUpdate()
    {
        if (_isLanding)
        {
            return;
        }

        bool isTimeOver = Time.time - _enterTime >= MaxFallDuration;

        if (boss.Movement.IsGrounded || isTimeOver)
        {
            Land();
            return;
        }

        boss.Movement.SetVelocity(new Vector2(0f, -boss.JumpAttackFallSpeed));
    }

    // 바닥에 닿았을 때: 멈추고, 주변 범위 충격을 한 번 내고, 충격음과 카메라 흔들림을 낸 뒤 착지 모습을 재생한다.
    private void Land()
    {
        _isLanding = true;
        boss.Movement.SetVelocity(Vector2.zero);

        // 접지 판정이 바닥에 닿기 직전에 켜질 수 있으므로, 중력을 다시 켜서 바닥에 내려앉게 한다.
        boss.Movement.SetGravityEnabled(true);

        boss.HitChecker.ResetHitTargets();
        int hitCount = boss.HitChecker.Hit(boss.JumpAttackImpactHitbox, boss.JumpAttackDamage, boss.Animation.FacingDirection);
        boss.ReducePatternGaugeByHits(hitCount);

        boss.PlayImpact();
        boss.Animation.Play(SoulKnightAnimation.JumpAttack, boss.JumpAttackLandProgress);
    }
}

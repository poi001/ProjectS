using UnityEngine;

// 슬라이딩. 구르기로 붙은 벽의 반대쪽 벽에 부딪힐 때까지 바닥을 따라 쭉 미끄러진다. Knight의 슬라이딩과 같은 방식이다. (Enemy_GDD 5.1, 6장)
// 방향은 구르기가 시작 전에 SetDirection으로 정한다. (구르기로 붙은 벽의 반대쪽)
// 시작할 때 패턴 게이지가 차고(Knight의 슬라이딩과 같은 양), 플레이어를 맞히면 준다.
// 미끄러지는 동안 플레이어가 몸에 닿으면 피해를 입는다. 한 번의 슬라이딩에서 한 번만 맞고, 서로 관통하므로 플레이어를 지나쳐 계속 미끄러진다.
// 미끄러지는 동안 중력을 끄고 가로로만 움직인다. (중력이 켜져 있으면 바닥 마찰 때문에 정한 속도보다 느려진다)
// 세 단계로 나뉜다.
// - 미끄러짐: 반대쪽 벽(또는 옆을 막는 지형)에 막힐 때까지. 막히면 충격음과 카메라 흔들림을 낸다.
// - 튕겨남: 벽에 부딪힌 뒤 누운 모습 그대로 위로 튕기며 포물선(대각선)을 그리고 맵 중앙(좌우 벽의 가운데)에 떨어진다. 이때는 피해를 주지 않는다.
//   중력을 켜고 속도만 정해 주는 물리 운동이다. 최고 높이(SlideBounceHeight)로 체공 시간을 정하고, 가로 속도는 중앙에 닿도록 계산한다.
// - 일어나기: 멈춘 자리에서 일어나는 모습을 재생한 뒤 Idle로 간다.
// 애니메이션은 Sliding 클립 하나를 나눠 쓴다. 일어나는 지점(SlideGetUpProgress) 직전에서 멈춘 채 미끄러지고 튕겨나며, 멈춘 뒤에 나머지를 재생한다.
public class SoulKnightSlideState : SoulKnightState
{
    // 벽에 막히지 않는 예외 상황(벽이 없는 맵 등)에서 미끄러짐을 끝내는 시간 (단위: 초)
    private const float MaxSlideDuration = 3f;
    // 맵 중앙에 닿지 못하는 예외 상황에서 튕겨남을 끝내는 시간 (단위: 초)
    private const float MaxBounceDuration = 2f;
    // 맵 중앙에 이 거리 안으로 들어오면 도착한 것으로 본다. (단위: 월드 거리)
    private const float ArriveTolerance = 0.02f;

    private enum Phase
    {
        Sliding,
        Bouncing,
        GettingUp
    }

    private Phase _phase;
    private float _direction = 1f;
    private float _phaseStartTime;
    private bool _hasBounceTarget;
    private float _bounceTargetX;
    // 튕겨나는 가로 속도의 크기 (단위: 월드 거리/초)
    private float _bounceSpeedX;

    public SoulKnightSlideState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 미끄러질 방향(오른쪽 1, 왼쪽 -1)을 정한다. 이 상태로 바꾸기 전에 호출한다.
    public void SetDirection(float direction)
    {
        _direction = Mathf.Sign(direction);
    }

    public override void Enter()
    {
        boss.PatternGauge.Add(boss.PatternGaugeGain);
        _phase = Phase.Sliding;
        _phaseStartTime = Time.time;
        _hasBounceTarget = boss.TryGetMapCenterX(out _bounceTargetX);

        boss.HitChecker.ResetHitTargets();
        boss.Animation.UpdateFacing(_direction);
        boss.Animation.Play(SoulKnightAnimation.Sliding);
        boss.Movement.SetGravityEnabled(false);
    }

    public override void Exit()
    {
        boss.Movement.SetGravityEnabled(true);
        boss.Movement.SetVelocity(Vector2.zero);
    }

    public override void LogicUpdate()
    {
        if (_phase == Phase.GettingUp)
        {
            if (boss.Animation.IsFinished(SoulKnightAnimation.Sliding))
            {
                boss.ChangeToRest();
            }

            return;
        }

        // 미끄러지거나 튕겨나는 동안에는 일어나는 모습이 나오지 않도록 누운 모습에서 멈춰 둔다.
        boss.Animation.HoldBefore(SoulKnightAnimation.Sliding, boss.SlideGetUpProgress);
    }

    public override void PhysicsUpdate()
    {
        if (_phase == Phase.Sliding)
        {
            UpdateSliding();
        }
        else if (_phase == Phase.Bouncing)
        {
            UpdateBouncing();
        }
    }

    private void UpdateSliding()
    {
        bool isTimeOver = Time.time - _phaseStartTime >= MaxSlideDuration;

        if (boss.Movement.IsBlocked(_direction) || isTimeOver)
        {
            HitWall();
            return;
        }

        boss.Movement.SetVelocity(new Vector2(_direction * boss.SlideSpeed, 0f));
        HitBody();
    }

    // 튕겨나는 동안: 중력으로 포물선을 그리며 날아가고, 바닥에 닿으면 일어난다.
    // 가로 속도는 시작할 때 정한 값을 유지하되, 맵 중앙을 지나치지 않도록 남은 거리만큼만 움직인다.
    // 옆이 막혀 있으면 가로로 밀지 않고 그대로 떨어진다.
    private void UpdateBouncing()
    {
        float bounceDirection = -_direction;
        bool isTimeOver = Time.time - _phaseStartTime >= MaxBounceDuration;

        if (boss.Movement.IsGrounded || isTimeOver)
        {
            StartGettingUp();
            return;
        }

        float remainingDistance = Mathf.Max(0f, (_bounceTargetX - boss.Movement.Position.x) * bounceDirection);
        float speedX = Mathf.Min(_bounceSpeedX, remainingDistance / Time.fixedDeltaTime);
        SetPatternVelocityX(bounceDirection * speedX);
    }

    // 몸에 닿은 플레이어에게 피해를 준다. 맞은 대상은 기록되므로 이번 슬라이딩에서 다시 맞지 않는다.
    // 플레이어를 맞히면 패턴 게이지가 준다.
    private void HitBody()
    {
        int hitCount = boss.HitChecker.Hit(boss.SlideBodyHitbox, boss.SlideDamage, _direction);
        boss.ReducePatternGaugeByHits(hitCount);
    }

    // 반대쪽 벽에 부딪혔을 때: 충격음과 카메라 흔들림을 내고 맵 중앙을 향해 포물선으로 튕겨난다.
    // 벽 앞에 붙어 있는 플레이어도 맞도록 멈춘 자리에서 몸 판정을 한 번 더 낸다.
    private void HitWall()
    {
        boss.Movement.SetVelocity(Vector2.zero);
        HitBody();
        boss.PlayImpact();

        float distance = Mathf.Abs(_bounceTargetX - boss.Movement.Position.x);

        if (!_hasBounceTarget || boss.SlideBounceHeight <= 0f || distance <= ArriveTolerance)
        {
            StartGettingUp();
            return;
        }

        // 최고 높이에서 중앙까지 거리의 절반을 지나는 점프 속도를 구한다. 오르고 내리는 궤적이 대칭이라 같은 높이의 바닥인 중앙에 떨어진다.
        Vector2 launchVelocity = boss.Movement.CalculateJumpVelocityTo(distance * 0.5f, boss.SlideBounceHeight, 0f);
        _bounceSpeedX = launchVelocity.x;

        _phase = Phase.Bouncing;
        _phaseStartTime = Time.time;
        boss.Movement.SetGravityEnabled(true);
        boss.Movement.Launch(new Vector2(-_direction * _bounceSpeedX, launchVelocity.y));
    }

    // 멈춘 자리에서 일어나는 모습을 재생한다. 끝나면 Idle로 간다.
    private void StartGettingUp()
    {
        _phase = Phase.GettingUp;
        boss.Movement.SetVelocity(Vector2.zero);
        boss.Animation.Play(SoulKnightAnimation.Sliding, boss.SlideGetUpProgress);
    }
}

using UnityEngine;

// 슬라이딩. 벽에 낮게(바닥 높이에) 매달려 있다가 반대쪽 벽에 부딪힐 때까지 바닥을 따라 쭉 미끄러진다. (Enemy_GDD 5.1)
// 방향은 벽타기가 시작 전에 SetDirection으로 정한다. (매달렸던 벽의 반대쪽)
// 속도와 피해량은 드롭킥 값을 그대로 쓴다. 미끄러지는 동안 플레이어가 몸에 닿으면 피해를 입는다. 한 번의 슬라이딩에서 한 번만 맞는다.
// 플레이어와는 서로 관통하므로 플레이어를 지나쳐 계속 미끄러진다. (GDD 6.7)
// 미끄러지는 동안 중력을 끄고 가로로만 움직인다. 중력이 켜져 있으면 바닥 마찰 때문에 드롭킥 속도보다 느려진다.
// 반대쪽 벽(또는 옆을 막는 지형)에 막히면 멈추고 종료 애니메이션을 재생한 뒤 Idle로 넘어간다.
// 애니메이션: Slide(반복하지 않음, 마지막 프레임에서 멈춘 채 미끄러짐) → Slide_End(벽에 부딪힌 뒤)
public class KnightSlideState : KnightState
{
    // 벽에 막히지 않는 예외 상황(벽이 없는 맵 등)에서 슬라이딩을 끝내는 시간 (단위: 초)
    private const float MaxSlideDuration = 3f;

    private float _direction = 1f;
    private float _enterTime;
    private bool _isEnding;

    public KnightSlideState(Knight owner, StateMachine<Enemy> stateMachine)
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
        knight.PatternGauge.Add(knight.DropKickGaugeGain);
        _enterTime = Time.time;
        _isEnding = false;

        knight.HitChecker.ResetHitTargets();
        knight.Animation.UpdateFacing(_direction);
        knight.Animation.Play(KnightAnimation.Slide);
        knight.Movement.SetGravityEnabled(false);
    }

    public override void Exit()
    {
        knight.Movement.SetGravityEnabled(true);
        knight.Movement.SetVelocity(Vector2.zero);
    }

    public override void LogicUpdate()
    {
        if (_isEnding && knight.Animation.IsFinished(KnightAnimation.SlideEnd))
        {
            knight.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        if (_isEnding)
        {
            return;
        }

        bool isTimeOver = Time.time - _enterTime >= MaxSlideDuration;

        if (knight.Movement.IsBlocked(_direction) || isTimeOver)
        {
            HitWall();
            return;
        }

        knight.Movement.SetVelocity(new Vector2(_direction * knight.DropKickSpeed, 0f));
        HitBody();
    }

    // 몸에 닿은 플레이어에게 피해를 준다. 맞은 대상은 기록되므로 이번 슬라이딩에서 다시 맞지 않는다.
    private void HitBody()
    {
        int hitCount = knight.HitChecker.Hit(knight.SlideBodyHitbox, knight.DropKickDamage, _direction);
        knight.ReducePatternGaugeByHits(hitCount);
    }

    // 반대쪽 벽에 부딪혔을 때: 멈추고 종료 애니메이션을 재생한다.
    // 벽 앞에 붙어 있는 플레이어도 맞도록 멈춘 자리에서 몸 판정을 한 번 더 낸다.
    private void HitWall()
    {
        _isEnding = true;
        knight.Movement.SetVelocity(Vector2.zero);
        HitBody();
        knight.Animation.Play(KnightAnimation.SlideEnd);
    }
}

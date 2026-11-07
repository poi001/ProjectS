using UnityEngine;

// 벽타기 후 슬라이딩. 벽에 매달려 있다가 플레이어 쪽 바닥으로 매우 빠르게 대각선으로 낙하한다. (GDD 6.6)
// 낙하 방향은 시작할 때 한 번만 정한다. 벽에 매달리는 순간 포착한 플레이어 위치에서 Knight 쪽으로 약간 앞의 바닥(Knight.SlideTargetPosition)을 향하는 방향이다.
// 낙하 중에는 중력을 끄고 그 방향으로 일정한 속도로 날아간다. 낙하 중 몸에 닿아도 피해는 없다.
// 바닥에 닿는(접지) 순간 그 자리에서 충격을 한 번 낸다. 날아간 방향으로 가로가 긴 직사각형 범위를 판정하고 충격 이펙트와 효과음을 낸다. (다단히트 없음)
// 충격 뒤에는 종료 애니메이션을 재생하고 Idle로 넘어간다.
// 바닥에 닿지 못하는 예외 상황은 SlideMaxDuration이 지나면 그 자리에서 충격을 내는 것으로 막는다.
// 애니메이션: Slide_Start → Slide_Loop(반복, 낙하 중) → Slide_End(충격 후)
public class KnightSlideState : KnightState
{
    private Vector2 _diveVelocity;
    private float _direction;
    private float _enterTime;
    private bool _isLooping;
    private bool _isEnding;

    public KnightSlideState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        _isLooping = false;
        _isEnding = false;

        // 낙하 지점을 향한 방향과 속도를 한 번만 정한다. 같은 x라면 바로 아래로 떨어진다.
        Vector2 toTarget = knight.SlideTargetPosition - knight.Movement.Position;
        Vector2 diveDirection = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.down;
        _diveVelocity = diveDirection * knight.SlideSpeed;

        // 날아가는 방향(좌우)은 낙하 지점 쪽이다. 같은 x라면 플레이어 쪽을 본다.
        _direction = Mathf.Abs(toTarget.x) > 0.01f ? Mathf.Sign(toTarget.x) : knight.DirectionToTarget;

        knight.Animation.UpdateFacing(_direction);
        knight.Animation.Play(KnightAnimation.SlideStart);
        knight.Movement.SetGravityEnabled(false);
    }

    public override void Exit()
    {
        knight.Movement.SetGravityEnabled(true);
        knight.Movement.SetVelocity(Vector2.zero);
    }

    public override void LogicUpdate()
    {
        if (_isEnding)
        {
            if (knight.Animation.IsFinished(KnightAnimation.SlideEnd))
            {
                knight.ChangeToRest();
            }

            return;
        }

        if (!_isLooping && knight.Animation.IsFinished(KnightAnimation.SlideStart))
        {
            _isLooping = true;
            knight.Animation.Play(KnightAnimation.SlideLoop);
        }
    }

    public override void PhysicsUpdate()
    {
        if (_isEnding)
        {
            return;
        }

        bool isTimeOver = Time.time - _enterTime >= knight.SlideMaxDuration;

        if (knight.Movement.IsGrounded || isTimeOver)
        {
            Land();
            return;
        }

        knight.Movement.SetVelocity(_diveVelocity);
    }

    // 바닥에 닿았을 때: 멈추고, 충격 판정을 한 번 내고, 이펙트와 효과음을 낸 뒤 종료 애니메이션을 재생한다.
    private void Land()
    {
        _isEnding = true;
        knight.Movement.SetVelocity(Vector2.zero);

        // 접지 판정이 바닥에 닿기 직전에 켜질 수 있으므로, 중력을 다시 켜서 바닥에 내려앉게 한다.
        knight.Movement.SetGravityEnabled(true);

        knight.HitChecker.ResetHitTargets();
        knight.HitChecker.Hit(knight.SlideImpactHitbox, knight.SlideImpactDamage, _direction);

        knight.SpawnSlideImpactEffect(_direction);
        knight.PlaySlideImpactSound();
        knight.Animation.Play(KnightAnimation.SlideEnd);
    }
}

using UnityEngine;

// 대시(돌진 몸통박치기). 준비 자세가 기억해 둔 플레이어 위치(SetAimPoint)까지 일정한 속도(DashSpeed)로 일직선 돌진한다.
// 플레이어가 공중에 있었다면 공중으로 날아간다. 돌진하는 동안 중력을 끄고, 그 자리에 도착하면 대시 마무리(DashEnd)로 넘어간다.
// (마무리에서 이어서 대시하면 그 자리에 떠 있고, 아니면 중력으로 떨어진다) 들어올 때마다 연속 대시 횟수를 하나 센다. (SkullDog.CountDash)
// 가는 길에 벽·바닥에 막히면 그 자리에서 끝난다. 막히지도 도착하지도 못하는 예외 상황에는 DashMaxDuration이 지나면 끝난다.
// 돌진하는 동안 플레이어가 몸에 닿으면 피해를 입는다. 한 번의 대시에서 한 번만 맞는다. (플레이어와는 서로 관통한다)
// 애니메이션: Dash(Sprite Sheet_7 → 8, 반복 없이 마지막 그림 유지)
// 연출: 돌진하는 동안 몸 뒤로 속도선(pixelFX status/haste-lines)을 돌진 방향으로 기울여 보여 준다. (SkullDog.PlayDashLines / StopDashLines)
public class SkullDogDashState : SkullDogState
{
    // 남은 거리가 이 값 이하면 도착한 것으로 본다. (단위: 월드 거리)
    private const float ArriveDistance = 0.001f;

    private Vector2 _aimPoint;
    private Vector2 _direction;
    // 보는 방향 (오른쪽 1, 왼쪽 -1). 몸 판정 범위의 좌우를 정한다.
    private float _facing;
    private float _remainingDistance;
    private float _enterTime;
    // 이번 물리 스텝으로 목표 지점이나 지형에 닿는 자리까지 옮겼는지. 다음 스텝에 끝난 것으로 처리한다.
    private bool _isArriving;

    public SkullDogDashState(SkullDog owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 돌진할 지점(루트 위치 기준)을 정한다. 이 상태로 바꾸기 전에 호출한다.
    public void SetAimPoint(Vector2 aimPoint)
    {
        _aimPoint = aimPoint;
    }

    public override void Enter()
    {
        Vector2 toAim = _aimPoint - skullDog.Movement.Position;
        _remainingDistance = toAim.magnitude;
        _facing = skullDog.Animation.FacingDirection;
        _direction = _remainingDistance > ArriveDistance ? toAim / _remainingDistance : new Vector2(_facing, 0f);

        if (Mathf.Abs(_direction.x) > 0.01f)
        {
            _facing = Mathf.Sign(_direction.x);
        }

        _enterTime = Time.time;
        _isArriving = _remainingDistance <= ArriveDistance;

        skullDog.CountDash();
        skullDog.HitChecker.ResetHitTargets();
        skullDog.Movement.SetGravityEnabled(false);
        skullDog.Movement.SetVelocity(Vector2.zero);
        skullDog.Animation.UpdateFacing(_facing);
        skullDog.Animation.Play(SkullDogAnimation.Dash);
        skullDog.PlayDashLines(_direction);
        skullDog.PlayDashSound();
    }

    // 돌진이 중간에 끊기는 경우(플레이어 사망, SkullDog 사망 등)에도 그 자리에 멈추고 중력을 되돌리며 속도선을 끈다.
    public override void Exit()
    {
        skullDog.StopDashLines();
        skullDog.Movement.SetGravityEnabled(true);
        skullDog.Movement.SetVelocity(Vector2.zero);
    }

    public override void PhysicsUpdate()
    {
        // 지금 자리의 몸 판정을 먼저 낸다. 도착한 자리에서도 마지막으로 판정한다.
        // 맞혔으면 패턴 게이지가 찬다.
        int hitCount = skullDog.HitChecker.Hit(skullDog.BodyHitbox, skullDog.DashDamage, _facing);
        skullDog.AddGaugeForHits(hitCount);

        if (_isArriving || Time.time - _enterTime >= skullDog.DashMaxDuration)
        {
            stateMachine.ChangeState(skullDog.DashEndState);
            return;
        }

        // 이번 스텝에 목표 지점이나 지형에 닿으면 그 자리까지만 옮기고, 다음 스텝에 끝난 것으로 처리한다.
        float stepDistance = Mathf.Min(skullDog.DashSpeed * Time.fixedDeltaTime, _remainingDistance);
        bool isBlocked = skullDog.CastBody(_direction, stepDistance, out float freeDistance);

        skullDog.Movement.SetVelocity(_direction * (freeDistance / Time.fixedDeltaTime));
        _remainingDistance -= freeDistance;
        _isArriving = isBlocked || _remainingDistance <= ArriveDistance;
    }
}

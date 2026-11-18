using UnityEngine;

// 발사(몸을 날리는 돌진). 준비 동작이 기억해 둔 플레이어 위치(SetAimPoint)까지 일정한 속도(LaunchSpeed)로 일직선으로 날아간다. (SkullDog 대시와 같은 방식)
// 플레이어가 공중에 있었다면 공중으로 날아간다. 날아가는 동안 중력을 끄고, 그 자리에 도착하면 멈춘다. (플레이어를 지나쳐 계속 가지 않는다)
// 가는 길에 벽·바닥에 막히면 그 자리에서 끝난다. 막히지도 도착하지도 못하는 예외 상황에는 LaunchMaxDuration이 지나면 끝난다.
// 날아가는 동안 플레이어가 몸에 닿으면 피해를 입는다. 한 번의 발사에서 한 번만 맞는다. (플레이어와는 서로 관통한다)
// 끝나면 Idle, 점프, 점프공격 중 하나로 넘어간다. (Mushroom.ChangeAfterLaunch) 공중에서 끝났다면 점프는 공중점프가 되고, Idle은 떨어져서 착지한 뒤에 쉰다.
// 애니메이션: Launch의 뒷부분(Mushroom-Attack_4 한 장 유지). 날아가는 쪽을 보며, 그러면 갓(머리)이 앞을 향한다.
//  끝난 뒤에도 그쪽을 본 채로 두며, 다음 패턴이 시작될 때 다시 플레이어 쪽을 본다.
// 연출: 날아가는 동안 몸 뒤로 속도선이 지나간다. 날아가는 방향으로 기울인다. (Mushroom.PlaySpeedLines / StopSpeedLines)
public class MushroomLaunchState : MushroomState
{
    // 남은 거리가 이 값 이하면 도착한 것으로 본다. (단위: 월드 거리)
    private const float ArriveDistance = 0.001f;

    private Vector2 _aimPoint;
    private Vector2 _direction;
    // 날아가는 가로 방향 (오른쪽 1, 왼쪽 -1). 보는 방향과 몸 판정 범위의 좌우를 정한다.
    private float _facing;
    private float _remainingDistance;
    private float _enterTime;
    // 이번 물리 스텝으로 목표 지점이나 지형에 닿는 자리까지 옮겼는지. 다음 스텝에 끝난 것으로 처리한다.
    private bool _isArriving;

    public MushroomLaunchState(Mushroom owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 날아갈 지점(루트 위치 기준)을 정한다. 이 상태로 바꾸기 전에 호출한다.
    public void SetAimPoint(Vector2 aimPoint)
    {
        _aimPoint = aimPoint;
    }

    public override void Enter()
    {
        Vector2 toAim = _aimPoint - mushroom.Movement.Position;
        _remainingDistance = toAim.magnitude;
        _facing = mushroom.GetFacingToward(toAim.x);
        _direction = _remainingDistance > ArriveDistance ? toAim / _remainingDistance : new Vector2(_facing, 0f);

        // 땅에 서 있으면 아래로는 갈 수 없으므로 바닥을 따라 가로로만 움직인다. (발 높이의 미세한 차이로 바닥에 바로 막히지 않게 한다)
        if (mushroom.Movement.IsGrounded && _direction.y < 0f)
        {
            _remainingDistance = Mathf.Abs(toAim.x);
            _direction = new Vector2(_remainingDistance > ArriveDistance ? Mathf.Sign(toAim.x) : _facing, 0f);
        }

        _enterTime = Time.time;
        _isArriving = _remainingDistance <= ArriveDistance;

        mushroom.HitChecker.ResetHitTargets();
        mushroom.ResetPlayerHit();
        mushroom.Movement.SetGravityEnabled(false);
        mushroom.Movement.SetVelocity(Vector2.zero);
        // 날아가는 쪽을 보면 그림(Attack_4)의 갓(머리)이 앞을 향한다.
        mushroom.Animation.UpdateFacing(_facing);
        mushroom.Animation.Play(MushroomAnimation.Launch, MushroomAnimation.LaunchFlyProgress);
        mushroom.PlaySpeedLines(_direction);
    }

    // 발사가 중간에 끊기는 경우(플레이어 사망, Mushroom 사망 등)에도 그 자리에 멈추고 중력을 되돌리며 속도선을 끈다.
    public override void Exit()
    {
        mushroom.StopSpeedLines();
        mushroom.Movement.SetGravityEnabled(true);
        mushroom.Movement.SetVelocity(Vector2.zero);
    }

    public override void PhysicsUpdate()
    {
        // 지금 자리의 몸 판정을 먼저 낸다. 도착한 자리에서도 마지막으로 판정한다.
        if (mushroom.HitChecker.Hit(mushroom.BodyHitbox, mushroom.LaunchDamage, _facing) > 0)
        {
            mushroom.MarkPlayerHit();
            mushroom.AddHitGauge();
        }

        if (!IsCurrentState)
        {
            return;
        }

        if (_isArriving || Time.time - _enterTime >= mushroom.LaunchMaxDuration)
        {
            mushroom.ChangeAfterLaunch();
            return;
        }

        // 이번 스텝에 목표 지점이나 지형에 닿으면 그 자리까지만 옮기고, 다음 스텝에 끝난 것으로 처리한다.
        float stepDistance = Mathf.Min(mushroom.LaunchSpeed * Time.fixedDeltaTime, _remainingDistance);
        bool isBlocked = mushroom.CastBody(_direction, stepDistance, out float freeDistance);

        mushroom.Movement.SetVelocity(_direction * (freeDistance / Time.fixedDeltaTime));
        _remainingDistance -= freeDistance;
        _isArriving = isBlocked || _remainingDistance <= ArriveDistance;
    }
}

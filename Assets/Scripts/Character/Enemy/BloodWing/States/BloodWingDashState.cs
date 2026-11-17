using UnityEngine;

// 대시(돌진 몸통박치기). 준비 자세가 끝나는 순간의 플레이어 위치 쪽으로 일정한 속도(DashSpeed)로 일직선 돌진한다.
// 조준점은 BloodWing.GetDashAimPoint: 서 있는 플레이어라면 보스 몸이 바닥에 닿는 자리가 플레이어 위치가 되도록 겨눈다.
// 플레이어 위치를 지나쳐도 같은 방향으로 계속 날아가 벽이나 바닥에 부딪히면 멈춘다. 그때 카메라가 흔들리고(충돌 피드백) 복귀(DashRecover)로 넘어간다.
// 돌진하는 동안 플레이어가 몸에 닿으면 피해를 입는다. 한 번의 대시에서 한 번만 맞는다. (Knight 슬라이딩과 같은 몸 충돌 판정)
// 돌진 중 몸이 바닥에 남은 운석에 닿으면 운석이 사라지고 카메라가 흔들리며 그 자리에서 기절(Stun)한다.
// 벽이나 바닥에 닿지 않는 예외 상황(위쪽으로 돌진 등)에는 DashMaxDuration이 지나면 멈추고 복귀한다.
// 애니메이션: Dash(FLYING 시트, 반복)
public class BloodWingDashState : BloodWingState
{
    private Vector2 _direction;
    // 보는 방향 (오른쪽 1, 왼쪽 -1). 몸 판정 범위의 좌우를 정한다.
    private float _facing;
    private float _enterTime;
    // 이번 물리 스텝으로 지형에 닿는 자리까지 옮겼는지. 다음 스텝에 부딪힌 것으로 처리한다.
    private bool _isReachingObstacle;

    public BloodWingDashState(BloodWing owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        Vector2 position = bloodWing.Movement.Position;
        Vector2 toTarget = bloodWing.GetDashAimPoint() - position;
        _direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.down;
        _facing = Mathf.Abs(_direction.x) > 0.01f ? Mathf.Sign(_direction.x) : bloodWing.Animation.FacingDirection;
        _enterTime = Time.time;
        _isReachingObstacle = false;

        bloodWing.HitChecker.ResetHitTargets();
        bloodWing.Animation.UpdateFacing(_facing);
        bloodWing.Animation.Play(BloodWingAnimation.Dash);
    }

    // 돌진이 중간에 끊기는 경우(플레이어 사망, BloodWing 사망 등)에도 그 자리에 멈춘다.
    public override void Exit()
    {
        bloodWing.Movement.SetVelocity(Vector2.zero);
    }

    public override void PhysicsUpdate()
    {
        // 지금 자리의 몸 판정을 먼저 낸다. 부딪힌 자리에서도 마지막으로 판정한다.
        bloodWing.HitChecker.Hit(bloodWing.DashBodyHitbox, bloodWing.DashDamage, _facing);

        // 기절로 바꾸지 못하는 경우(사망 등)에는 운석을 남겨 두고 돌진을 계속한다.
        if (bloodWing.TryFindRemainingMeteor(_facing, out BloodWingMeteor meteor) && bloodWing.ChangeToStun())
        {
            meteor.Consume();
            bloodWing.PlayImpactFeedbacks();
            return;
        }

        if (_isReachingObstacle)
        {
            bloodWing.PlayImpactFeedbacks();
            stateMachine.ChangeState(bloodWing.DashRecoverState);
            return;
        }

        if (Time.time - _enterTime >= bloodWing.DashMaxDuration)
        {
            stateMachine.ChangeState(bloodWing.DashRecoverState);
            return;
        }

        // 이번 스텝에 벽·바닥에 닿으면 닿기 직전까지만 옮기고, 다음 스텝에 부딪힌 것으로 처리한다.
        float stepDistance = bloodWing.DashSpeed * Time.fixedDeltaTime;
        _isReachingObstacle = bloodWing.CastBody(_direction, stepDistance, out float freeDistance);
        bloodWing.Movement.SetVelocity(_direction * (freeDistance / Time.fixedDeltaTime));
    }
}

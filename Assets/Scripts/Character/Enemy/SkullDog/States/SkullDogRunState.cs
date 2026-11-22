using UnityEngine;

// 런. 플레이어가 있는 쪽으로 벽에 부딪힐 때까지 계속 달린다.
//  - 속도: 시작 속도(RunStartSpeed)에서 출발해 달린 시간에 비례해 빨라지고(RunAcceleration), 최고 속도(RunMaxSpeed)에서 멈춘다.
//  - 관성: 플레이어가 반대쪽으로 넘어가도 바로 돌아서지 못한다. 속도를 1초에 RunTurnAcceleration만큼만 바꿀 수 있어서,
//    달리던 쪽으로 미끄러지며 속도를 줄인 뒤 돌아서서 다시 속도를 올린다. 그림은 실제로 움직이는 쪽을 본다.
//  - 시작 유예: 런을 시작하고 RunGraceDuration(0.5초) 동안에는 벽에 닿아도 스턴에 걸리지 않고 몸에 닿은 플레이어도 피해를 입지 않는다.
//    벽 쪽으로는 가지 못하므로 그 동안 벽에 막히면 속도를 0으로 둔다. (벽 앞에서 속도가 쌓였다가 한꺼번에 튀어나가지 않게 한다) 시간이 지나면 아래 판정이 정상으로 들어간다.
//  - 벽에 부딪히면 카메라가 흔들리고 스턴(Stun)에 걸린다. 스턴 시간은 부딪힌 순간의 속도로 정해진다. (SkullDog.GetStunDuration)
//  - 달리는 동안 플레이어가 몸에 닿으면 피해를 입는다. 맞은 대상 기록을 매번 비우므로, 플레이어의 피격 무적 시간이 끝난 뒤에도 닿아 있으면 다시 맞는다.
//    (무적 중에는 CharacterVital이 피해를 막는다. 플레이어와는 서로 관통한다) 맞힐 때마다 패턴 게이지가 찬다. (SkullDog.AddGaugeForHits)
//  - 폭주 런(패턴 게이지 기믹, SkullDog.IsGimmickActive인 동안): 벽에 부딪혀도 스턴에 걸리지 않고 속도도 줄지 않는다.
//    벽 앞에서는 나아가지 못할 뿐 속도 값은 그대로 두므로, 속도는 방향을 바꿀 때(관성)만 달라진다. 나머지는 런과 같다.
//    기믹이 끝나면(게이지 0%) 달리던 속도 그대로 일반 런이 되어, 다음에 벽에 막히면(이미 막혀 있었다면 바로) 스턴에 걸린다.
//    폭주 런이 중간에 끊기면(플레이어 사망, SkullDog 사망 등) 기믹도 함께 끝낸다. (SkullDog.CancelGimmick)
// 달리는 동안 중력을 끄고 가로로만 움직인다. 중력이 켜져 있으면 바닥 마찰 때문에 정한 속도보다 느려진다. (Knight 슬라이딩과 같다)
// 애니메이션: Run(Sprite Sheet_6 ~ 10, 반복)
// 연출: 달리는 동안 뒷발에서 먼지(pixelFX impacts/run-dust, Mushroom 런과 같은 효과)가 뒤로 흩날린다. (SkullDog.PlayRunDust / StopRunDust)
public class SkullDogRunState : SkullDogState
{
    // 플레이어와의 가로 거리가 이 값 이하면 달리는 방향을 바꾸지 않는다. 바로 위아래에 있을 때 방향이 매 스텝 뒤집히지 않게 한다. (단위: 월드 거리)
    private const float DirectionDeadZone = 0.1f;
    // 속도가 이 값 이하면 멈춘 것으로 보고 벽 충돌을 확인하지 않는다. (단위: 월드 거리/초)
    private const float MinMovingSpeed = 0.01f;

    // 달리려는 방향 (오른쪽 1, 왼쪽 -1). 플레이어가 있는 쪽이다.
    private float _direction;
    // 지금 가로 속도 (오른쪽이 양수). 관성 때문에 달리려는 방향과 부호가 다를 수 있다.
    private float _velocityX;
    // 런을 시작한 뒤 지난 시간. 물리 스텝마다 더한다.
    private float _elapsedTime;

    public SkullDogRunState(SkullDog owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _direction = skullDog.IsTargetDead ? skullDog.Animation.FacingDirection : skullDog.DirectionToTarget;
        _velocityX = _direction * skullDog.RunStartSpeed;
        _elapsedTime = 0f;

        skullDog.Movement.SetGravityEnabled(false);
        skullDog.Animation.UpdateFacing(_direction);
        skullDog.Animation.Play(SkullDogAnimation.Run);
        skullDog.PlayRunDust();
    }

    // 런이 중간에 끊기는 경우(플레이어 사망, SkullDog 사망 등)에도 그 자리에 멈추고 중력을 되돌린다.
    // 폭주 런 도중이었다면 기믹도 함께 끝낸다. (일반 런이면 아무것도 하지 않는다)
    public override void Exit()
    {
        skullDog.CancelGimmick();
        skullDog.StopRunDust();
        skullDog.Movement.SetGravityEnabled(true);
        skullDog.Movement.SetVelocity(Vector2.zero);
    }

    public override void PhysicsUpdate()
    {
        _elapsedTime += Time.fixedDeltaTime;
        UpdateDirection();

        float topSpeed = Mathf.Min(skullDog.RunMaxSpeed, skullDog.RunStartSpeed + skullDog.RunAcceleration * _elapsedTime);
        _velocityX = Mathf.MoveTowards(_velocityX, _direction * topSpeed, skullDog.RunTurnAcceleration * Time.fixedDeltaTime);

        float speed = Mathf.Abs(_velocityX);
        float moveDirection = speed > MinMovingSpeed ? Mathf.Sign(_velocityX) : _direction;

        bool isGrace = _elapsedTime < skullDog.RunGraceDuration;
        // 이번 스텝에 실제로 움직일 속도. 폭주 런이 벽에 막혔을 때만 속도 값(_velocityX)과 달라진다.
        float appliedVelocityX = _velocityX;

        if (speed > MinMovingSpeed && skullDog.Movement.IsBlocked(moveDirection))
        {
            if (skullDog.IsGimmickActive)
            {
                // 폭주 런: 스턴 없이 벽 앞에 머문다. 벽을 밀지 않도록 실제로는 멈추되 속도 값은 줄이지 않는다.
                appliedVelocityX = 0f;
            }
            else if (!isGrace)
            {
                HitWall(moveDirection, speed);
                return;
            }
            else
            {
                // 유예 중에는 스턴 없이 벽 앞에 머문다. 속도가 쌓이지 않게 0으로 둔다.
                _velocityX = 0f;
                appliedVelocityX = 0f;
            }
        }

        skullDog.Movement.SetVelocity(new Vector2(appliedVelocityX, 0f));
        skullDog.Animation.UpdateFacing(moveDirection);

        if (!isGrace)
        {
            HitBody(moveDirection);
        }
    }

    // 달리려는 방향을 플레이어가 있는 쪽으로 맞춘다.
    private void UpdateDirection()
    {
        if (skullDog.IsTargetDead)
        {
            return;
        }

        float offsetX = skullDog.Target.Movement.Position.x - skullDog.Movement.Position.x;

        if (Mathf.Abs(offsetX) > DirectionDeadZone)
        {
            _direction = Mathf.Sign(offsetX);
        }
    }

    // 몸에 닿은 플레이어에게 피해를 준다. 맞은 대상 기록을 매번 비워, 무적 시간이 끝난 뒤에도 닿아 있으면 다시 맞게 한다.
    // 맞힌 만큼 패턴 게이지가 찬다. (기믹 중에는 차지 않는다)
    private void HitBody(float facingDirection)
    {
        // 이 타격으로 플레이어가 죽으면 판정 도중에 런이 끊겨 기믹이 취소되므로, 폭주 런의 타격이었는지는 판정 전에 봐 둔다.
        bool wasGimmickActive = skullDog.IsGimmickActive;

        skullDog.HitChecker.ResetHitTargets();
        int hitCount = skullDog.HitChecker.Hit(skullDog.BodyHitbox, skullDog.RunDamage, facingDirection);

        if (!wasGimmickActive)
        {
            skullDog.AddGaugeForHits(hitCount);
        }
    }

    // 벽에 부딪혔을 때: 멈추고 부딪힌 속도에 맞는 시간 동안 스턴에 걸리며 카메라가 흔들린다.
    // 벽 앞에 붙어 있는 플레이어도 맞도록 멈춘 자리에서 몸 판정을 한 번 더 낸다.
    // 스턴으로 바꾸지 못한 경우(사망 등)에는 카메라를 흔들지 않는다. 벽에 막힌 채 매 스텝 흔들림이 반복되지 않게 한다.
    private void HitWall(float moveDirection, float impactSpeed)
    {
        _velocityX = 0f;
        skullDog.Movement.SetVelocity(Vector2.zero);
        HitBody(moveDirection);

        if (skullDog.ChangeToStun(impactSpeed))
        {
            skullDog.PlayImpactFeedbacks();
        }
    }
}

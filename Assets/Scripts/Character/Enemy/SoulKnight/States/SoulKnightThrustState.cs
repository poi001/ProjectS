using UnityEngine;

// 공격 3타(찌르기). 공격 2타 뒤에 이어지거나, 공격 묶음을 골랐는데 플레이어가 사거리 밖이면 이 공격부터 시작한다. (Enemy_GDD 6.9)
// 시작할 때 플레이어 쪽으로 돌아서서, 찌르는 구간(ThrustEndProgress까지) 동안 정해진 거리(ThrustDistance)를 훅 돌진한다.
// 남은 거리와 찌르는 구간의 남은 시간으로 속도를 매 물리 프레임 다시 계산해, 찌르는 구간이 끝날 때 딱 그 거리만큼 이동한다.
// 벽에 막히면 이동만 멈추고 상태와 애니메이션은 끝까지 진행한다.
// 애니메이션 이벤트 없이 슬라이딩처럼 찌르는 동안 매 물리 프레임 몸 판정을 내며, 한 번의 찌르기에서 한 번만 맞는다. 맞히면 게이지가 준다.
// 끝나면 딜레이 없이 내려치기(Smash)로 이어진다.
public class SoulKnightThrustState : SoulKnightState
{
    private float _direction;
    private float _startX;
    // 찌르는 구간이 끝나는 시각을 정했는지와 그 시각 (물리 시간 기준)
    private bool _hasEndTime;
    private float _endTime;

    public SoulKnightThrustState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        boss.AddPatternGaugeByAttack(boss.AttackGaugeGain);

        if (!boss.IsTargetDead)
        {
            boss.FaceTarget();
        }

        _direction = boss.Animation.FacingDirection;
        _startX = boss.Movement.Position.x;
        _hasEndTime = false;
        boss.HitChecker.ResetHitTargets();
        boss.Animation.Play(SoulKnightAnimation.Thrust);
    }

    public override void Exit()
    {
        boss.Movement.SetVelocityX(0f);
    }

    public override void LogicUpdate()
    {
        if (boss.Animation.IsFinished(SoulKnightAnimation.Thrust))
        {
            stateMachine.ChangeState(boss.SmashState);
        }
    }

    public override void PhysicsUpdate()
    {
        // 찌르는 구간이 끝나는 시각은 찌르기 애니메이션으로 바뀐 것을 처음 확인했을 때 한 번만 정한다.
        // Animator의 진행도는 화면 프레임마다 갱신되므로, 물리 프레임마다 남은 시간을 구하려면 시각으로 계산해야 한다.
        if (!_hasEndTime)
        {
            float timeUntilEnd = boss.Animation.GetTimeUntil(SoulKnightAnimation.Thrust, boss.ThrustEndProgress);

            if (timeUntilEnd < 0f)
            {
                boss.Movement.SetVelocityX(0f);
                return;
            }

            _hasEndTime = true;
            _endTime = Time.time + timeUntilEnd;
        }

        if (Time.time >= _endTime)
        {
            boss.Movement.SetVelocityX(0f);
            return;
        }

        float travelled = Mathf.Abs(boss.Movement.Position.x - _startX);
        float remainingDistance = Mathf.Max(0f, boss.ThrustDistance - travelled);
        float remainingTime = Mathf.Max(_endTime - Time.time, Time.fixedDeltaTime);

        // 벽에 막히면 SetPatternVelocityX가 0으로 바꾼다. 상태는 그대로 진행한다.
        SetPatternVelocityX(_direction * remainingDistance / remainingTime);

        int hitCount = boss.HitChecker.Hit(boss.ThrustBodyHitbox, boss.ThrustDamage, _direction);
        boss.ReducePatternGaugeByHits(hitCount);
    }
}

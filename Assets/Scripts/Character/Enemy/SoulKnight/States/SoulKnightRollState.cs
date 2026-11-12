using UnityEngine;

// 구르기. 자신과 가장 먼 벽 쪽으로 굴러 벽에 밀착한 뒤, 딜레이 없이 반대쪽(가까운) 벽을 향한 슬라이딩으로 이어진다. (Enemy_GDD 6장)
// 벽은 시작할 때 한 번만 고른다. (SoulKnight.TryChooseRollWall: 자신과 먼 벽 → 같으면 플레이어와 먼 벽 → 그래도 같으면 오른쪽 벽)
// 구르기 애니메이션이 한 번 재생되는 동안 벽에 딱 도착하도록, 남은 거리와 남은 재생 시간으로 속도를 매 물리 프레임 다시 계산한다.
// 벽 앞(몸이 밀착하는 지점)까지만 이동하므로 벽을 뚫거나 밀지 않는다.
// 구르는 동안에는 피격 판정을 꺼서 어떤 피해도 받지 않는다.
// 좌우에 벽이 없는 예외 상황에서는 제자리에서 구른 뒤 플레이어 쪽으로 슬라이딩한다.
public class SoulKnightRollState : SoulKnightState
{
    // 벽 앞 도착 지점에 이 거리 안으로 들어오면 도착한 것으로 본다. (단위: 월드 거리)
    private const float ArriveTolerance = 0.02f;
    // 애니메이션이 끝난 뒤에도 벽에 닿지 못했을 때 남은 거리를 한 번에 맞추는 물리 프레임 수의 한도
    private const int MaxSnapSteps = 3;

    private bool _hasWall;
    // 구르는 방향 = 벽이 있는 방향 (오른쪽 1, 왼쪽 -1)
    private float _wallDirection;
    private bool _isAnimationFinished;
    private int _snapSteps;
    // 구르기 애니메이션이 끝나는 시각을 정했는지와 그 시각 (물리 시간 기준)
    private bool _hasEndTime;
    private float _endTime;

    public SoulKnightRollState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _isAnimationFinished = false;
        _snapSteps = 0;
        _hasEndTime = false;
        _hasWall = boss.TryChooseRollWall(out _wallDirection);

        if (!_hasWall)
        {
            _wallDirection = -boss.DirectionToTarget;
        }

        boss.SetHurtboxEnabled(false);
        boss.Animation.UpdateFacing(_wallDirection);
        boss.Animation.Play(SoulKnightAnimation.Roll);
    }

    public override void Exit()
    {
        boss.SetHurtboxEnabled(true);
        boss.Movement.SetVelocityX(0f);
    }

    public override void LogicUpdate()
    {
        if (!_isAnimationFinished && boss.Animation.IsFinished(SoulKnightAnimation.Roll))
        {
            _isAnimationFinished = true;
        }
    }

    public override void PhysicsUpdate()
    {
        float remainingDistance = _hasWall ? boss.GetBodyDistanceToWall(_wallDirection) : 0f;

        if (_isAnimationFinished)
        {
            // 애니메이션이 끝났다. 아직 벽에 닿지 못했다면 남은 거리를 바로 맞추고 슬라이딩으로 넘어간다.
            if (remainingDistance <= ArriveTolerance || _snapSteps >= MaxSnapSteps)
            {
                boss.Movement.SetVelocityX(0f);
                boss.ChangeToSlide(-_wallDirection);
                return;
            }

            _snapSteps++;
            boss.Movement.SetVelocityX(_wallDirection * remainingDistance / Time.fixedDeltaTime);
            return;
        }

        // 애니메이션이 끝나는 시각은 구르기 애니메이션으로 바뀐 것을 처음 확인했을 때 한 번만 정한다.
        // Animator의 진행도는 화면 프레임마다 갱신되므로, 물리 프레임마다 남은 시간을 구하려면 시각으로 계산해야 한다.
        if (!_hasEndTime)
        {
            float animationRemainingTime = boss.Animation.GetRemainingTime(SoulKnightAnimation.Roll);

            if (animationRemainingTime < 0f)
            {
                boss.Movement.SetVelocityX(0f);
                return;
            }

            _hasEndTime = true;
            _endTime = Time.time + animationRemainingTime;
        }

        // 이미 벽에 붙어 있으면 움직이지 않는다.
        if (remainingDistance <= 0f)
        {
            boss.Movement.SetVelocityX(0f);
            return;
        }

        float remainingTime = Mathf.Max(_endTime - Time.time, Time.fixedDeltaTime);
        boss.Movement.SetVelocityX(_wallDirection * remainingDistance / remainingTime);
    }
}

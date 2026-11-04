// 공격 1회/2회 공통 베이스. 공격 애니메이션을 재생하고, 정해진 진행도마다 플레이어 쪽으로 살짝 전진한다.
// 전진을 시작할 때마다 플레이어 방향을 다시 확인한다. 애니메이션이 끝나면 구르기 또는 다음 패턴으로 넘어간다. (GDD 6.6)
// 데미지 판정은 아직 구현하지 않았다.
public abstract class KnightAttackBaseState : KnightState
{
    // 현재 진행 중인(또는 마지막으로 시작한) 전진 번호. 아직 시작 전이면 -1
    private int _stepIndex;
    private float _stepDirection;

    protected KnightAttackBaseState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected abstract int AnimationHash { get; }
    protected abstract int StepCount { get; }

    // stepIndex번째 전진을 시작하는 공격 애니메이션 진행도(0~1)
    protected abstract float GetStepStartTime(int stepIndex);

    public override void Enter()
    {
        _stepIndex = -1;
        knight.Animation.Play(AnimationHash);
    }

    public override void Exit()
    {
        knight.Movement.SetVelocityX(0f);
    }

    public override void LogicUpdate()
    {
        if (knight.Animation.IsFinished(AnimationHash))
        {
            knight.ChangeToPatternAfterAttack();
        }
    }

    public override void PhysicsUpdate()
    {
        float normalizedTime = knight.Animation.GetNormalizedTime(AnimationHash);
        UpdateStep(normalizedTime);

        bool isStepping = _stepIndex >= 0 && normalizedTime < GetStepStartTime(_stepIndex) + knight.AttackStepLength;
        SetPatternVelocityX(isStepping ? _stepDirection * knight.AttackStepSpeed : 0f);
    }

    // 다음 전진 시점에 도달하면 플레이어 쪽을 바라보고 전진을 시작한다.
    private void UpdateStep(float normalizedTime)
    {
        int nextStepIndex = _stepIndex + 1;

        if (nextStepIndex >= StepCount || normalizedTime < GetStepStartTime(nextStepIndex))
        {
            return;
        }

        _stepIndex = nextStepIndex;
        _stepDirection = knight.DirectionToTarget;
        knight.FaceTarget();
    }
}

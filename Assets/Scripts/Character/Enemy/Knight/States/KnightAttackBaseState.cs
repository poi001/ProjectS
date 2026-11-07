// 공격 1회/2회 공통 베이스. 공격 애니메이션을 재생하고, 칼을 휘두르는 시점마다 플레이어 쪽으로 살짝 전진한다.
// 공격 클립은 휘두르기 전 준비 프레임이 멈춰 있다가 휘두를 때 빠르게 재생되며, 준비 자세가 멈춰 있는 동안에는 전진하지 않는다.
// 공격을 시작할 때 플레이어 방향을 한 번만 정하고, 공격이 끝날 때까지 그 방향을 바라보며 그 방향으로만 전진한다.
// (공격 2회의 두 번째 휘두름에서도 방향을 다시 정하지 않는다.) 애니메이션이 끝나면 구르기 또는 Idle로 넘어간다. (GDD 6.6)
// 휘두름(KnightSwing)마다 시작 시점에 전진과 판정이 함께 시작된다. 전진은 StepLength 동안, 판정은 HitEndTime까지 이어진다.
// 판정 구간에서는 매 물리 프레임 공격 판정을 내며, 휘두름 하나당 같은 대상은 한 번만 맞는다.
// 자식 클래스는 재생할 애니메이션과 휘두름 목록만 정한다. 휘두름은 시작 시점이 빠른 순서로 들어 있어야 한다.
public abstract class KnightAttackBaseState : KnightState
{
    // 현재 진행 중인(또는 마지막으로 시작한) 전진의 휘두름 번호. 아직 시작 전이면 -1
    private int _stepIndex;
    // 가장 최근에 판정이 나간 휘두름 번호. 새 휘두름의 판정 구간에 들어갈 때 맞은 대상 기록을 비우는 기준이다. 아직 없으면 -1
    private int _activeSwingIndex;
    // 공격을 시작할 때 정한 방향. 공격이 끝날 때까지 바뀌지 않는다. (오른쪽 1, 왼쪽 -1)
    private float _stepDirection;

    protected KnightAttackBaseState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected abstract int AnimationHash { get; }
    // 이 공격의 휘두름 목록. 인스펙터에서 배열 크기가 바뀔 수 있으므로 매번 Knight에서 읽는다.
    protected abstract KnightSwing[] Swings { get; }

    public override void Enter()
    {
        _stepIndex = -1;
        _activeSwingIndex = -1;
        _stepDirection = knight.DirectionToTarget;
        knight.FaceTarget();
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
        KnightSwing[] swings = Swings;
        float normalizedTime = knight.Animation.GetNormalizedTime(AnimationHash);
        UpdateStep(swings, normalizedTime);

        bool isStepping = _stepIndex >= 0 && normalizedTime < swings[_stepIndex].HitStartTime + swings[_stepIndex].StepLength;
        SetPatternVelocityX(isStepping ? _stepDirection * knight.AttackStepSpeed : 0f);

        UpdateHit(swings, normalizedTime);
    }

    // 지금 진행도가 어떤 휘두름의 판정 구간 안이면 그 휘두름의 공격 판정을 낸다.
    // 새 휘두름의 구간에 처음 들어갈 때 맞은 대상 기록을 비워, 공격 2회에서 두 휘두름이 각각 맞힐 수 있게 한다.
    private void UpdateHit(KnightSwing[] swings, float normalizedTime)
    {
        for (int i = 0; i < swings.Length; i++)
        {
            KnightSwing swing = swings[i];

            if (normalizedTime < swing.HitStartTime || normalizedTime >= swing.HitEndTime)
            {
                continue;
            }

            if (i != _activeSwingIndex)
            {
                _activeSwingIndex = i;
                knight.HitChecker.ResetHitTargets();
            }

            int hitCount = knight.HitChecker.Hit(swing.Hitbox, knight.Stats.AttackPower * swing.DamageMultiplier, _stepDirection);
            knight.ReducePatternGaugeByHits(hitCount);
        }
    }

    // 다음 휘두름의 시작 시점에 도달하면 전진을 시작하고 패턴 게이지를 채운다. 방향은 공격 시작 때 정한 것을 그대로 쓴다.
    // 게이지는 휘두름마다 따로 차므로 공격 2회는 두 번 찬다.
    private void UpdateStep(KnightSwing[] swings, float normalizedTime)
    {
        int nextStepIndex = _stepIndex + 1;

        if (nextStepIndex >= swings.Length || normalizedTime < swings[nextStepIndex].HitStartTime)
        {
            return;
        }

        _stepIndex = nextStepIndex;
        knight.PatternGauge.Add(knight.SwingGaugeGain);
    }
}

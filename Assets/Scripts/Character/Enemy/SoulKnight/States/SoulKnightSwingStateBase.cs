// 휘두르기 공격(Attack1·Attack2·Smash) 공통 베이스. 각성 후 공격 4타 중 칼을 휘두르는 공격이다. (Enemy_GDD 6.9)
// 시작할 때 패턴 게이지가 차고, 애니메이션 이벤트(OnAttackHitFrame)가 올 때마다 휘두름 범위로 한 번 판정한다. 플레이어를 맞히면 게이지가 준다.
// 판정 시점은 클립에 넣은 이벤트가 정하며, 이벤트 하나마다 맞은 대상 기록을 비우므로 이벤트마다 한 번씩 맞을 수 있다.
// 휘두름의 전진 구간(진행도) 동안 바라보는 방향으로 살짝 전진한다. 벽에 막히면 멈춘다.
// 애니메이션이 끝나면 딜레이 없이 다음 상태로 넘어간다. 자식 클래스는 재생할 애니메이션, 휘두름 설정, 방향 전환 여부, 다음 상태만 정한다.
public abstract class SoulKnightSwingStateBase : SoulKnightState
{
    // 공격하는 방향. 시작할 때 정하며 공격이 끝날 때까지 바뀌지 않는다. (오른쪽 1, 왼쪽 -1)
    private float _direction;

    protected SoulKnightSwingStateBase(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected abstract int AnimationHash { get; }
    // 인스펙터에서 값이 바뀔 수 있으므로 매번 SoulKnight에서 읽는다.
    protected abstract SoulKnightSwing Swing { get; }
    // 시작할 때 플레이어 쪽으로 돌아서는지. false면 직전에 보던 방향으로 휘두른다.
    protected abstract bool FacesTargetOnEnter { get; }
    // 휘두르면서 전진하는지
    protected virtual bool CanStep => true;

    // 애니메이션이 끝났을 때 넘어갈 상태로 바꾼다.
    protected abstract void ChangeToNextState();

    public override void Enter()
    {
        boss.AddPatternGaugeByAttack(boss.AttackGaugeGain);

        if (FacesTargetOnEnter && !boss.IsTargetDead)
        {
            boss.FaceTarget();
        }

        _direction = boss.Animation.FacingDirection;
        boss.HitChecker.ResetHitTargets();
        boss.Animation.AttackHitFrame += OnAttackHitFrame;
        boss.Animation.Play(AnimationHash);
    }

    public override void Exit()
    {
        boss.Animation.AttackHitFrame -= OnAttackHitFrame;
        boss.Movement.SetVelocityX(0f);
    }

    public override void LogicUpdate()
    {
        if (boss.Animation.IsFinished(AnimationHash))
        {
            ChangeToNextState();
        }
    }

    public override void PhysicsUpdate()
    {
        SoulKnightSwing swing = Swing;
        float progress = boss.Animation.GetNormalizedTime(AnimationHash);
        bool isStepping = CanStep && progress >= swing.StepStartProgress && progress < swing.StepEndProgress;
        SetPatternVelocityX(isStepping ? _direction * boss.AttackStepSpeed : 0f);
    }

    // 클립의 애니메이션 이벤트(OnAttackHitFrame) 시점에 휘두름 범위로 한 번 판정한다.
    private void OnAttackHitFrame()
    {
        SoulKnightSwing swing = Swing;
        boss.HitChecker.ResetHitTargets();
        int hitCount = boss.HitChecker.Hit(swing.Hitbox, boss.Stats.AttackPower * swing.DamageMultiplier, _direction);
        boss.ReducePatternGaugeByHits(hitCount);
    }
}

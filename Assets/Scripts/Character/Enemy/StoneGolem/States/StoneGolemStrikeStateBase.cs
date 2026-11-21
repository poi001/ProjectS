// 공격·스킬 공통 베이스. 제자리에서 내려치는 애니메이션을 한 번 재생하고, 정해진 구간에 앞쪽 범위로 공격 판정을 낸다.
// 시작할 때 플레이어 방향을 한 번만 정하고, 끝날 때까지 그 방향을 바라본다. 애니메이션이 끝나면 Idle로 쉬어간다.
// 판정 구간(StoneGolemStrike의 시작~끝 진행도)에서는 매 물리 프레임 판정을 내며, 같은 대상은 한 번만 맞는다.
// 자식 클래스는 재생할 애니메이션과 내려치기 설정만 정한다.
public abstract class StoneGolemStrikeStateBase : StoneGolemState
{
    // 시작할 때 정한 방향. 끝날 때까지 바뀌지 않는다. (오른쪽 1, 왼쪽 -1)
    private float _direction;
    // 이번 내려치기의 판정 구간에 이미 들어갔는지. 처음 들어갈 때 맞은 대상 기록을 비운다.
    private bool _hasHitStarted;

    protected StoneGolemStrikeStateBase(StoneGolem owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected abstract int AnimationHash { get; }
    // 이 상태의 내려치기 설정. 인스펙터에서 값이 바뀔 수 있으므로 매번 StoneGolem에서 읽는다.
    protected abstract StoneGolemStrike Strike { get; }

    public override void Enter()
    {
        _hasHitStarted = false;
        _direction = golem.DirectionToTarget;
        golem.FaceTarget();
        golem.Movement.SetVelocityX(0f);
        golem.Animation.Play(AnimationHash);
    }

    public override void LogicUpdate()
    {
        if (golem.Animation.IsFinished(AnimationHash))
        {
            golem.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        golem.Movement.SetVelocityX(0f);

        StoneGolemStrike strike = Strike;
        float normalizedTime = golem.Animation.GetNormalizedTime(AnimationHash);

        if (normalizedTime < strike.HitStartTime || normalizedTime >= strike.HitEndTime)
        {
            return;
        }

        if (!_hasHitStarted)
        {
            _hasHitStarted = true;
            golem.HitChecker.ResetHitTargets();
        }

        golem.HitChecker.Hit(strike.Hitbox, golem.Stats.AttackPower * strike.DamageMultiplier, _direction);
    }
}

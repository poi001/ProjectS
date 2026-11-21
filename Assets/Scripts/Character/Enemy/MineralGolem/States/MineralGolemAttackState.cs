// 공격. 제자리에서 팔을 들었다가 주먹으로 바닥을 내리찍는다. 주먹이 닿는 구간에 앞쪽 범위로 판정이 나간다. (MineralGolem의 '공격' 항목, 계열 공용)
// 시작할 때 플레이어 방향을 한 번만 정하고, 끝날 때까지 그 방향을 바라본다. 애니메이션이 끝나면 Idle로 쉬어간다.
// 판정 구간(Attack Hit Start Time ~ Attack Hit End Time)에서는 매 물리 프레임 판정을 내며, 같은 대상은 한 번만 맞는다.
// 애니메이션: Attack(Golem_1_attack 0 ~ 10, 한 번 재생)
public class MineralGolemAttackState : MineralGolemState
{
    // 시작할 때 정한 방향. 끝날 때까지 바뀌지 않는다. (오른쪽 1, 왼쪽 -1)
    private float _direction;
    // 이번 공격의 판정 구간에 이미 들어갔는지. 처음 들어갈 때 맞은 대상 기록을 비운다.
    private bool _hasHitStarted;

    public MineralGolemAttackState(MineralGolem owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _hasHitStarted = false;
        _direction = golem.DirectionToTarget;
        golem.FaceTarget();
        golem.Movement.SetVelocityX(0f);
        golem.Animation.Play(MineralGolemAnimation.Attack);
    }

    public override void LogicUpdate()
    {
        if (golem.Animation.IsFinished(MineralGolemAnimation.Attack))
        {
            golem.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        golem.Movement.SetVelocityX(0f);

        float normalizedTime = golem.Animation.GetNormalizedTime(MineralGolemAnimation.Attack);

        if (normalizedTime < golem.AttackHitStartTime || normalizedTime >= golem.AttackHitEndTime)
        {
            return;
        }

        if (!_hasHitStarted)
        {
            _hasHitStarted = true;
            golem.HitChecker.ResetHitTargets();
        }

        golem.HitChecker.Hit(golem.AttackHitbox, golem.AttackDamage, _direction);
    }
}

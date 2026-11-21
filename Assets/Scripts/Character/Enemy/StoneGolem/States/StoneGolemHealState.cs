// 힐. 제자리에 멈춰 힐 애니메이션을 한 번 재생하고, 몸이 번쩍이는 시점(StoneGolem의 Heal Time)에 체력을 회복(HealAmount)한다.
// 애니메이션이 끝나면 Idle로 쉬어간다. 회복 전에 상태가 끊기면(사망, 플레이어 사망 등) 체력은 오르지 않는다.
// 발동 조건과 피격 시 끊김 여부는 아직 정해지지 않았다. 지금은 맞아도 끊기지 않는다.
// 애니메이션: Heal(gollux_healing 0 ~ 24, 한 번 재생)
public class StoneGolemHealState : StoneGolemState
{
    // 이번 힐에서 이미 회복했는지. 회복은 한 번만 한다.
    private bool _hasHealed;

    public StoneGolemHealState(StoneGolem owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _hasHealed = false;
        golem.Movement.SetVelocityX(0f);
        golem.Animation.Play(StoneGolemAnimation.Heal);
    }

    public override void LogicUpdate()
    {
        if (!_hasHealed && golem.Animation.GetNormalizedTime(StoneGolemAnimation.Heal) >= golem.HealTime)
        {
            _hasHealed = true;
            golem.Vital.Heal(golem.HealAmount);
        }

        if (golem.Animation.IsFinished(StoneGolemAnimation.Heal))
        {
            golem.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        golem.Movement.SetVelocityX(0f);
    }
}

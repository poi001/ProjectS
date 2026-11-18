using UnityEngine;

// 가만히 있는 상태. 패턴 사이에 플레이어가 공격할 시간을 주고, 정해진 시간이 지나면 다음 패턴으로 넘어간다.
// 플레이어가 죽어 있는 동안에는 다음 패턴으로 넘어가지 않고 계속 머문다. (GDD 6.6)
// 보스가 Enemy.CanCountIdleTime을 false로 두는 동안(Mushroom은 공중)에는 시간을 세지 않고, true가 되는 순간부터 센다.
public class EnemyIdleState : EnemyState
{
    private float _duration;
    private float _enterTime;

    public EnemyIdleState(Enemy owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 다음 진입 시 Idle에 머무를 시간. 진입 전에 정한다. (Enemy.ChangeToIdle)
    public void SetDuration(float duration)
    {
        _duration = duration;
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        owner.Movement.SetVelocityX(0f);
        owner.Animation.Play(EnemyAnimation.Idle);
    }

    public override void LogicUpdate()
    {
        if (owner.IsTargetDead)
        {
            return;
        }

        // 쉬는 도중에 끼어드는 보스 고유 전환(SoulKnight의 체력 50% 이하 기도 등)이 있으면 그쪽을 따른다.
        if (owner.TryInterruptIdle())
        {
            return;
        }

        // 시간을 셀 수 없는 동안(공중 등)에는 기준 시각을 계속 현재로 옮겨, 셀 수 있게 되는 순간부터 센다.
        if (!owner.CanCountIdleTime)
        {
            _enterTime = Time.time;
            return;
        }

        if (Time.time - _enterTime >= _duration)
        {
            owner.ChangeToNextPattern();
        }
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.SetVelocityX(0f);
    }
}

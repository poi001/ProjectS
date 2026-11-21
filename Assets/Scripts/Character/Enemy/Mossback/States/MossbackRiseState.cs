using UnityEngine;

// 대기 높이 복귀. 스매시가 끝났거나 스턴이 풀린 자리에서 대기 높이(HoverY)까지 일정한 속도(RiseSpeed)로 수직으로 돌아간다.
// 돌아가는 동안에는 몸에 닿아도 피해가 없다. 다 돌아가면 스매시 뒤에는 바로 플라이로, 스턴 뒤에는 Idle로 넘어간다. (Mossback.ChangeToRise가 정한다)
// 기획의 상태 목록에는 없는, 공중 보스가 제자리로 돌아가기 위한 보조 상태다. (BloodWing의 DashRecover와 같은 역할)
// 애니메이션: Idle (복귀 전용 그림이 정해지면 바꾼다)
public class MossbackRiseState : MossbackState
{
    // 대기 높이에 도착했다고 보는 남은 거리 (단위: 월드 거리)
    private const float ArriveDistance = 0.001f;

    // 도착한 뒤 Idle로 쉬는지. false면 바로 플라이로 넘어간다.
    private bool _endsWithIdle;

    public MossbackRiseState(Mossback owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 이 상태로 바꾸기 직전에 호출한다. (Mossback.ChangeToRise)
    public void SetEndsWithIdle(bool endsWithIdle)
    {
        _endsWithIdle = endsWithIdle;
    }

    public override void Enter()
    {
        mossback.Animation.Play(EnemyAnimation.Idle);
    }

    // 복귀가 중간에 끊기는 경우(플레이어 사망, Mossback 사망 등)에도 그 자리에 멈춘다.
    public override void Exit()
    {
        mossback.Movement.SetVelocity(Vector2.zero);
    }

    public override void PhysicsUpdate()
    {
        float positionY = mossback.Movement.Position.y;

        if (Mathf.Abs(mossback.HoverY - positionY) <= ArriveDistance)
        {
            mossback.Movement.SetVelocity(Vector2.zero);

            if (_endsWithIdle)
            {
                mossback.ChangeToIdle(mossback.StunRecoverIdleDuration);
            }
            else
            {
                stateMachine.ChangeState(mossback.FlyState);
            }

            return;
        }

        // 한 스텝 뒤에 있어야 할 높이를 향해 속도를 정하므로 대기 높이를 지나치지 않고 정확히 멈춘다.
        float nextY = Mathf.MoveTowards(positionY, mossback.HoverY, mossback.RiseSpeed * Time.fixedDeltaTime);
        mossback.Movement.SetVelocity(new Vector2(0f, (nextY - positionY) / Time.fixedDeltaTime));
    }
}

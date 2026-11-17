using UnityEngine;

// 대시 준비 자세(전조). 정해진 시간(DashReadyDuration, 0.5초) 동안 플레이어 쪽을 바라보며 살짝 위로(DashReadyRiseDistance) 올라가는 준비 자세를 보여 준다.
// 플레이어가 보고 피할 시간을 준다. 올라가기를 마치면 돌진(Dash)으로 넘어가고, 돌진 방향은 올라간 자리에서 그 순간의 플레이어 위치로 정한다.
// 애니메이션: DashReady(FLYING_0 한 장 고정)
public class BloodWingDashReadyState : BloodWingState
{
    private readonly BloodWingLinearMove _move = new BloodWingLinearMove();

    public BloodWingDashReadyState(BloodWing owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        Vector2 position = bloodWing.Movement.Position;

        _move.Begin(position, position + Vector2.up * bloodWing.DashReadyRiseDistance, bloodWing.DashReadyDuration);
        bloodWing.Movement.SetVelocity(Vector2.zero);
        bloodWing.Animation.Play(BloodWingAnimation.DashReady);
        FaceTarget();
    }

    // 준비가 중간에 끊기는 경우(플레이어 사망, BloodWing 사망 등)에도 그 자리에 멈춘다.
    public override void Exit()
    {
        bloodWing.Movement.SetVelocity(Vector2.zero);
    }

    public override void LogicUpdate()
    {
        FaceTarget();
    }

    public override void PhysicsUpdate()
    {
        if (_move.Step(bloodWing.Movement))
        {
            stateMachine.ChangeState(bloodWing.DashState);
        }
    }

    private void FaceTarget()
    {
        if (!bloodWing.IsTargetDead)
        {
            bloodWing.FaceTarget();
        }
    }
}

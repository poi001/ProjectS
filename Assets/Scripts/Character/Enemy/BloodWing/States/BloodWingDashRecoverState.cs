using UnityEngine;

// 대시 복귀. 돌진이 벽·바닥에 부딪혀 끝났거나 기절이 풀린 자리에서, 가까운 쪽 대기 위치(두 대기 위치와의 거리가 같으면 오른쪽)까지
// 정해진 시간(DashReturnDuration, 0.5초) 동안 일직선으로 일정하게 돌아간다. 돌아가는 동안에는 몸에 닿아도 피해가 없다.
// 다 돌아가면 화면 안쪽을 보고 Idle로 쉰다.
// 애니메이션: 대시(FLYING 반복)를 다시 시작하지 않고 그대로 이어서 재생한다. (기절에서 올 때는 기절 상태가 대시 애니메이션으로 바꿔 둔다)
public class BloodWingDashRecoverState : BloodWingState
{
    private readonly BloodWingLinearMove _move = new BloodWingLinearMove();

    public BloodWingDashRecoverState(BloodWing owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        Vector2 start = bloodWing.Movement.Position;
        Vector2 perch = bloodWing.GetNearestPerch(start.x);

        _move.Begin(start, perch, bloodWing.DashReturnDuration);
        bloodWing.Animation.UpdateFacing(perch.x - start.x);
    }

    // 복귀가 중간에 끊기는 경우(플레이어 사망, BloodWing 사망 등)에도 그 자리에 멈춘다.
    public override void Exit()
    {
        bloodWing.Movement.SetVelocity(Vector2.zero);
    }

    public override void PhysicsUpdate()
    {
        if (!_move.Step(bloodWing.Movement))
        {
            return;
        }

        bloodWing.FaceFieldCenter();
        bloodWing.ChangeToIdle(bloodWing.PatternInterval);
    }
}

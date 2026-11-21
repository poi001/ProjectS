using UnityEngine;

// 이동. 플레이어 쪽을 보며 고정된 속도(Move Speed)로 걷는다. 피해는 없다. (MineralGolem 계열 공용)
// Move Duration이 지나면 Idle로 쉬어간다. 플레이어와의 가로 거리가 Move Stop Distance 이하이면 더 다가가지 않고 제자리에서 걷는 모션만 재생한다.
// 걷는 도중 플레이어가 반대쪽으로 넘어가면 그쪽으로 돌아선다.
// 애니메이션: Move(Golem_1_walk 0 ~ 9, 반복)
public class MineralGolemMoveState : MineralGolemState
{
    private float _enterTime;

    public MineralGolemMoveState(MineralGolem owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        golem.FaceTarget();
        golem.Animation.Play(MineralGolemAnimation.Move);
    }

    public override void Exit()
    {
        golem.Movement.SetVelocityX(0f);
    }

    public override void LogicUpdate()
    {
        if (Time.time - _enterTime >= golem.MoveDuration)
        {
            golem.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        if (golem.DistanceToTarget <= golem.MoveStopDistance)
        {
            golem.Movement.SetVelocityX(0f);
            return;
        }

        golem.FaceTarget();
        SetPatternVelocityX(golem.DirectionToTarget * golem.MoveSpeed);
    }
}

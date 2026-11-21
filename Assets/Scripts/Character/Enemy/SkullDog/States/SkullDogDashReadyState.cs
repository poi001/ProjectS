using UnityEngine;

// 대시 준비 자세(전조). 들어오는 순간의 플레이어 위치를 기억하고 그쪽을 바라본 채, 정해진 시간(DashReadyDuration, 0.5초) 동안 제자리에서 준비 자세를 보여 준다.
// 플레이어가 보고 피할 시간을 준다. 준비가 끝나면 기억해 둔 위치로 돌진(Dash)한다. 준비 중에 플레이어가 움직여도 겨누는 자리는 바뀌지 않는다.
// 이어서 하는 대시(SkullDogDashEndState)는 공중에서 준비할 수 있으므로, 준비하는 동안 중력을 끄고 그 자리에 떠 있다. (바닥에서 준비할 때도 같다)
// 애니메이션: DashReady(Sprite Sheet_6 한 장 고정)
public class SkullDogDashReadyState : SkullDogState
{
    private Vector2 _aimPoint;
    private float _enterTime;

    public SkullDogDashReadyState(SkullDog owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        _aimPoint = skullDog.GetDashAimPoint();

        skullDog.Movement.SetGravityEnabled(false);
        skullDog.Movement.SetVelocity(Vector2.zero);
        skullDog.Animation.UpdateFacing(_aimPoint.x - skullDog.Movement.Position.x);
        skullDog.Animation.Play(SkullDogAnimation.DashReady);
    }

    // 준비가 중간에 끊기는 경우(플레이어 사망, SkullDog 사망 등)에도 중력을 되돌린다. 공중이었다면 떨어진다.
    public override void Exit()
    {
        skullDog.Movement.SetGravityEnabled(true);
    }

    public override void LogicUpdate()
    {
        if (Time.time - _enterTime < skullDog.DashReadyDuration)
        {
            return;
        }

        skullDog.DashState.SetAimPoint(_aimPoint);
        stateMachine.ChangeState(skullDog.DashState);
    }

    public override void PhysicsUpdate()
    {
        skullDog.Movement.SetVelocity(Vector2.zero);
    }
}

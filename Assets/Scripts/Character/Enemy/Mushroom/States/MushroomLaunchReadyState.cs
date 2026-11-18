using UnityEngine;

// 발사 준비 동작(전조). 정해진 시간(LaunchReadyDuration, 0.5초) 동안 제자리에서 준비 동작을 보여 준다.
// 준비 동작의 절반(LaunchAimTime)이 지났을 때의 플레이어 위치를 기억하고 그쪽을 본다. 그 뒤에는 플레이어가 움직여도 겨누는 자리가 바뀌지 않는다.
// 준비가 끝나면 기억해 둔 위치로 발사(Launch)한다. 이 상태에서는 몸에 닿아도 피해가 없다.
// 애니메이션: Launch의 앞부분(Mushroom-Attack_0 ~ 3, 0.5초). 준비 시간이 그보다 길면 3에서 멈춰 둔다.
public class MushroomLaunchReadyState : MushroomState
{
    private float _enterTime;
    // 플레이어 위치를 기억했는지 여부와 그 위치(루트 위치 기준)
    private bool _hasAim;
    private Vector2 _aimPoint;

    public MushroomLaunchReadyState(Mushroom owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        _hasAim = false;

        mushroom.Movement.SetVelocityX(0f);

        if (!mushroom.IsTargetDead)
        {
            mushroom.FaceTarget();
        }

        mushroom.Animation.Play(MushroomAnimation.Launch);
    }

    public override void LogicUpdate()
    {
        float elapsedTime = Time.time - _enterTime;

        if (!_hasAim && elapsedTime >= mushroom.LaunchAimTime)
        {
            CaptureAim();
        }

        if (elapsedTime < mushroom.LaunchReadyDuration)
        {
            // 준비가 끝나기 전에 날아가는 그림(Attack_4)으로 넘어가지 않게 한다.
            mushroom.Animation.HoldBefore(MushroomAnimation.Launch, MushroomAnimation.LaunchFlyProgress);
            return;
        }

        if (!_hasAim)
        {
            CaptureAim();
        }

        mushroom.LaunchState.SetAimPoint(_aimPoint);
        stateMachine.ChangeState(mushroom.LaunchState);
    }

    public override void PhysicsUpdate()
    {
        mushroom.Movement.SetVelocityX(0f);
    }

    // 지금 플레이어의 위치를 기억하고 그쪽을 본다.
    private void CaptureAim()
    {
        _hasAim = true;
        _aimPoint = mushroom.GetAimPoint();
        mushroom.Animation.UpdateFacing(mushroom.GetFacingToward(_aimPoint.x - mushroom.Movement.Position.x));
    }
}

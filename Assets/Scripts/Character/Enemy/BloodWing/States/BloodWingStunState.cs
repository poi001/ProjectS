using UnityEngine;

// 기절. 돌진 중 바닥에 남은 운석에 부딪히면 들어온다. (BloodWingDashState → BloodWing.ChangeToStun)
// 공중에서 기절하면 중력을 켜서 바닥으로 툭 떨어지고, 바닥에 닿으면 그 자리에 멈춘다. 이미 바닥에 있으면 바로 멈춘다.
// 정해진 시간(StunDuration, 3초) 동안 바닥에서 기절해 있는다. 시간은 바닥에 닿은 순간부터 센다.
// 기절 모션은 반복하지 않고 한 번 재생한 뒤 마지막 그림을 유지한다. (떨어지는 동안에도 재생한다)
// 기절이 끝나면 가까운 쪽 대기 위치로 돌아가(DashRecover) Idle로 쉰다.
// 기절 VFX(BloodWing.StunVfxPrefab, 공용 StatusVfx_Stun)가 연결되어 있으면 머리 위에 띄운다.
// BloodWing은 평소 중력을 끄고 떠 있으므로, 나갈 때(Exit) 중력을 다시 끄고 속도를 0으로 한다. (다음 이동은 속도로 직접 움직인다)
public class BloodWingStunState : BloodWingState
{
    private float _landTime;
    // 아직 바닥에 닿지 않고 떨어지는 중인지
    private bool _isFalling;

    public BloodWingStunState(BloodWing owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        bloodWing.Movement.SetVelocity(Vector2.zero);
        bloodWing.Animation.Play(BloodWingAnimation.Stun);
        bloodWing.ShowStatusVfx(bloodWing.StunVfxPrefab);

        // 공중이면 중력을 켜서 떨어뜨리고, 이미 바닥이면 그 자리에서 바로 기절한다.
        _isFalling = !bloodWing.Movement.IsGrounded;
        _landTime = Time.time;

        if (_isFalling)
        {
            bloodWing.Movement.SetGravityEnabled(true);
        }
    }

    // 기절이 중간에 끊기는 경우(플레이어 사망, BloodWing 사망 등)에도 VFX를 끄고, 다시 중력을 끈 채 멈춘다.
    public override void Exit()
    {
        bloodWing.HideStatusVfx(bloodWing.StunVfxPrefab);
        bloodWing.Movement.SetGravityEnabled(false);
        bloodWing.Movement.SetVelocity(Vector2.zero);
    }

    public override void LogicUpdate()
    {
        if (_isFalling || Time.time - _landTime < bloodWing.StunDuration)
        {
            return;
        }

        // 복귀하는 동안에는 대시 애니메이션(FLYING)을 보여 준다.
        bloodWing.Animation.Play(BloodWingAnimation.Dash);
        stateMachine.ChangeState(bloodWing.DashRecoverState);
    }

    public override void PhysicsUpdate()
    {
        if (!_isFalling)
        {
            bloodWing.Movement.SetVelocity(Vector2.zero);
            return;
        }

        // 가로로는 흘러가지 않고 수직으로만 떨어진다.
        bloodWing.Movement.SetVelocityX(0f);

        if (!bloodWing.Movement.IsGrounded)
        {
            return;
        }

        // 바닥에 닿았다: 멈추고 중력을 끈다. 기절 시간은 지금부터 센다.
        _isFalling = false;
        _landTime = Time.time;
        bloodWing.Movement.SetGravityEnabled(false);
        bloodWing.Movement.SetVelocity(Vector2.zero);
    }
}

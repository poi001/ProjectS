using UnityEngine;

// 기절. 돌진 중 바닥에 남은 운석에 부딪히면 들어온다. (BloodWingDashState → BloodWing.ChangeToStun)
// 정해진 시간(StunDuration, 3초) 동안 그 자리에 멈춰 있다. 기절 모션은 반복하지 않고 한 번 재생한 뒤 마지막 그림을 유지한다.
// 기절이 끝나면 가까운 쪽 대기 위치로 돌아가(DashRecover) Idle로 쉰다.
// 기절 VFX(BloodWing.StunVfxPrefab)가 연결되어 있으면 머리 위에 띄운다. (지금은 비워 두어 VFX 없이 기절한다)
public class BloodWingStunState : BloodWingState
{
    private float _enterTime;

    public BloodWingStunState(BloodWing owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        bloodWing.Movement.SetVelocity(Vector2.zero);
        bloodWing.Animation.Play(BloodWingAnimation.Stun);
        bloodWing.ShowStatusVfx(bloodWing.StunVfxPrefab);
    }

    // 기절이 중간에 끊기는 경우(플레이어 사망, BloodWing 사망 등)에도 VFX를 끈다.
    public override void Exit()
    {
        bloodWing.HideStatusVfx(bloodWing.StunVfxPrefab);
    }

    public override void LogicUpdate()
    {
        if (Time.time - _enterTime < bloodWing.StunDuration)
        {
            return;
        }

        // 복귀하는 동안에는 대시 애니메이션(FLYING)을 보여 준다.
        bloodWing.Animation.Play(BloodWingAnimation.Dash);
        stateMachine.ChangeState(bloodWing.DashRecoverState);
    }

    public override void PhysicsUpdate()
    {
        bloodWing.Movement.SetVelocity(Vector2.zero);
    }
}

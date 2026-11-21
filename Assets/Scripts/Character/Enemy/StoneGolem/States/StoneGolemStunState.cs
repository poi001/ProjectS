using UnityEngine;

// 스턴(기절). 정해진 시간(StoneGolem의 Stun Duration) 동안 그 자리에 멈춰 있다.
// 스턴하는 동안 머리 위에 기절 VFX(StoneGolem.StunVfxPrefab, 공용 StatusVfx_Stun)를 띄운다. 스턴이 끝나면 Idle로 쉬어간다.
// 발동 조건은 아직 정해지지 않았다. (StoneGolem.ChangeToStun)
// 애니메이션: Stun(gollux_hit 0 ~ 3, 반복)
public class StoneGolemStunState : StoneGolemState
{
    private float _enterTime;

    public StoneGolemStunState(StoneGolem owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        golem.Movement.SetVelocityX(0f);
        golem.Animation.Play(StoneGolemAnimation.Stun);
        golem.ShowStatusVfx(golem.StunVfxPrefab);
    }

    // 스턴이 중간에 끊기는 경우(플레이어 사망, StoneGolem 사망 등)에도 VFX를 끈다.
    public override void Exit()
    {
        golem.HideStatusVfx(golem.StunVfxPrefab);
    }

    public override void LogicUpdate()
    {
        if (Time.time - _enterTime >= golem.StunDuration)
        {
            golem.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        golem.Movement.SetVelocityX(0f);
    }
}

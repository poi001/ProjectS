using UnityEngine;

// 스턴(기절). 정해진 시간(StunDuration) 동안 그 자리에 멈춰 있다가 Idle로 쉬어간다.
// 스턴하는 동안 머리 위에 기절 VFX(Mushroom.StunVfxPrefab, 공용 StatusVfx_Stun)를 띄운다.
// 반응속도 기믹(MushroomReactionState)에서 Mushroom이 닿기 전에 플레이어가 먼저 때렸을 때만 걸린다. (Mushroom.ChangeToStun)
// 스턴하는 동안 패턴 게이지는 차지 않는다.
// 애니메이션: Stun(Mushroom-Hit_0 ~ 4, 반복)
public class MushroomStunState : MushroomState
{
    private float _enterTime;

    public MushroomStunState(Mushroom owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        mushroom.Movement.SetVelocityX(0f);
        mushroom.Animation.Play(MushroomAnimation.Stun);
        mushroom.ShowStatusVfx(mushroom.StunVfxPrefab);
    }

    // 스턴이 중간에 끊기는 경우(플레이어 사망, Mushroom 사망 등)에도 VFX를 끈다.
    public override void Exit()
    {
        mushroom.HideStatusVfx(mushroom.StunVfxPrefab);
    }

    public override void LogicUpdate()
    {
        if (Time.time - _enterTime >= mushroom.StunDuration)
        {
            mushroom.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        mushroom.Movement.SetVelocityX(0f);
    }
}

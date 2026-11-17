using UnityEngine;

// 스턴(기절). 런으로 벽에 부딪히면 들어온다. (SkullDogRunState → SkullDog.ChangeToStun)
// 정해진 시간 동안 그 자리에 멈춰 있다. 시간은 벽에 부딪힌 순간의 속도가 빠를수록 길며, 들어오기 전에 SetDuration으로 정한다.
// 스턴하는 동안 머리 위에 기절 VFX(SkullDog.StunVfxPrefab, 공용 StatusVfx_Stun)를 띄운다. 스턴이 끝나면 Idle로 쉬어간다.
// 애니메이션: Stun(Sprite Sheet_11 ~ 14, 반복)
public class SkullDogStunState : SkullDogState
{
    private float _duration;
    private float _enterTime;

    public SkullDogStunState(SkullDog owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 다음 진입 시 스턴에 걸려 있을 시간. 이 상태로 바꾸기 전에 호출한다. (단위: 초)
    public void SetDuration(float duration)
    {
        _duration = duration;
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        skullDog.Movement.SetVelocityX(0f);
        skullDog.Animation.Play(SkullDogAnimation.Stun);
        skullDog.ShowStatusVfx(skullDog.StunVfxPrefab);
    }

    // 스턴이 중간에 끊기는 경우(플레이어 사망, SkullDog 사망 등)에도 VFX를 끈다.
    public override void Exit()
    {
        skullDog.HideStatusVfx(skullDog.StunVfxPrefab);
    }

    public override void LogicUpdate()
    {
        if (Time.time - _enterTime >= _duration)
        {
            skullDog.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        skullDog.Movement.SetVelocityX(0f);
    }
}

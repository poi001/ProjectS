using UnityEngine;

// 스턴. 조건이 아직 정해지지 않아 패턴에는 연결되어 있지 않고, Mossback.ChangeToStun(컨텍스트 메뉴 "Stun (플레이 중)")으로만 들어온다.
// 공중에서 걸리면 중력을 켜서 바닥으로 떨어지고, 바닥에 닿으면 그 자리에 멈춘다. 이미 바닥에 있으면 바로 멈춘다. (BloodWing과 같은 방식)
// 정해진 시간(StunDuration) 동안 바닥에서 멈춰 있는다. 시간은 바닥에 닿은 순간부터 센다. 끝나면 대기 높이로 돌아가(Rise) Idle로 쉰 뒤 플라이로 넘어간다.
// 스턴 VFX(Mossback.StunVfxPrefab, 공용 StatusVfx_Stun)가 연결되어 있으면 머리 위에 띄운다.
// Mossback은 평소 중력을 끄고 떠 있으므로, 나갈 때(Exit) 중력을 다시 끄고 속도를 0으로 한다.
// 애니메이션: Stun(Enemy3-Hit 시트, 반복)
public class MossbackStunState : MossbackState
{
    private float _landTime;
    // 아직 바닥에 닿지 않고 떨어지는 중인지
    private bool _isFalling;

    public MossbackStunState(Mossback owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        mossback.Movement.SetVelocity(Vector2.zero);
        mossback.Animation.Play(MossbackAnimation.Stun);
        mossback.ShowStatusVfx(mossback.StunVfxPrefab);

        // 공중이면 중력을 켜서 떨어뜨리고, 이미 바닥이면 그 자리에서 바로 멈춘다.
        _isFalling = !mossback.Movement.IsGrounded;
        _landTime = Time.time;

        if (_isFalling)
        {
            mossback.Movement.SetGravityEnabled(true);
        }
    }

    // 스턴이 중간에 끊기는 경우(플레이어 사망, Mossback 사망 등)에도 VFX를 끄고, 다시 중력을 끈 채 멈춘다.
    public override void Exit()
    {
        mossback.HideStatusVfx(mossback.StunVfxPrefab);
        mossback.Movement.SetGravityEnabled(false);
        mossback.Movement.SetVelocity(Vector2.zero);
    }

    public override void LogicUpdate()
    {
        if (_isFalling || Time.time - _landTime < mossback.StunDuration)
        {
            return;
        }

        mossback.ChangeToRise(true);
    }

    public override void PhysicsUpdate()
    {
        if (!_isFalling)
        {
            mossback.Movement.SetVelocity(Vector2.zero);
            return;
        }

        // 가로로는 흘러가지 않고 수직으로만 떨어진다.
        mossback.Movement.SetVelocityX(0f);

        if (!mossback.Movement.IsGrounded)
        {
            return;
        }

        // 바닥에 닿았다: 멈추고 중력을 끈다. 스턴 시간은 지금부터 센다.
        _isFalling = false;
        _landTime = Time.time;
        mossback.Movement.SetGravityEnabled(false);
        mossback.Movement.SetVelocity(Vector2.zero);
    }
}

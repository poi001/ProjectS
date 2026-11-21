using UnityEngine;

// 스매시(낙하). 지금 자리에서 바닥까지 일정한 속도(SmashFallSpeed)로 수직으로 떨어진다. 바닥에 닿으면 착지 충격을 내고 스매시 끝으로 넘어간다.
// 떨어지는 동안 플레이어가 몸에 닿으면 피해를 입는다. (Knight 슬라이딩과 같은 몸 충돌 판정)
// 바닥에 닿은 순간에는 발밑에 착지 효과와 함께 퍼지는 착지 충격 판정이 나간다. (Mossback.StartLandingImpact)
// 몸통과 착지 충격은 같은 판정 기록을 쓰므로 한 번의 스매시에서 한 번만 맞는다.
// 바닥에 닿지 않는 예외 상황에는 SmashMaxDuration이 지나면 그 자리에서 대기 높이로 돌아간다. (착지 충격 없음)
// 애니메이션: Smash(Enemy3-AttackSmashLoop 시트, 반복, 컨트롤러 상태 Speed 3)
public class MossbackSmashState : MossbackState
{
    // 보는 방향 (오른쪽 1, 왼쪽 -1). 몸 판정 범위의 좌우를 정한다.
    private float _facing;
    private float _enterTime;

    public MossbackSmashState(Mossback owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _facing = mossback.Animation.FacingDirection;
        _enterTime = Time.time;

        mossback.HitChecker.ResetHitTargets();
        mossback.Animation.Play(MossbackAnimation.Smash);
    }

    // 낙하가 중간에 끊기는 경우(플레이어 사망, Mossback 사망 등)에도 그 자리에 멈춘다.
    public override void Exit()
    {
        mossback.Movement.SetVelocity(Vector2.zero);
    }

    public override void PhysicsUpdate()
    {
        // 지금 자리의 몸 판정을 먼저 낸다. 바닥에 닿은 자리에서도 마지막으로 판정한다.
        mossback.HitChecker.Hit(mossback.SmashBodyHitbox, mossback.SmashDamage, _facing);

        if (mossback.Movement.IsGrounded)
        {
            mossback.StartLandingImpact();
            stateMachine.ChangeState(mossback.SmashEndState);
            return;
        }

        if (Time.time - _enterTime >= mossback.SmashMaxDuration)
        {
            mossback.ChangeToRise(false);
            return;
        }

        mossback.Movement.SetVelocity(new Vector2(0f, -mossback.SmashFallSpeed));
    }
}

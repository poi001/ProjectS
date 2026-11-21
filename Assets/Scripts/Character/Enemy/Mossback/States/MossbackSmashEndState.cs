using UnityEngine;

// 스매시 끝. 바닥에 내려앉은 자리에서 SmashEnd 애니메이션을 한 번 재생하고, 끝나면 대기 높이로 돌아간 뒤(Rise) 플라이로 넘어간다.
// 바닥에 머무는 시간은 애니메이션 길이가 정한다. 이 상태 자체에는 피해가 없다. (착지 충격 판정은 Mossback이 따로 낸다)
// 애니메이션: SmashEnd(Enemy3-SmashEnd 시트, 반복 없음, 컨트롤러 상태 Speed 3)
public class MossbackSmashEndState : MossbackState
{
    public MossbackSmashEndState(Mossback owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        mossback.Movement.SetVelocity(Vector2.zero);
        mossback.Animation.Play(MossbackAnimation.SmashEnd);
    }

    public override void LogicUpdate()
    {
        if (mossback.Animation.IsFinished(MossbackAnimation.SmashEnd))
        {
            mossback.ChangeToRise(false);
        }
    }

    public override void PhysicsUpdate()
    {
        mossback.Movement.SetVelocity(Vector2.zero);
    }
}

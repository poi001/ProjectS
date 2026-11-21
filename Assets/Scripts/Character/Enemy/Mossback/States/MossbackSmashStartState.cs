using UnityEngine;

// 스매시 시작(전조). 플라이 중 플레이어가 수직 아래쯤에 왔을 때 들어온다.
// 제자리에 멈춰 플레이어 쪽을 보며 SmashStart 애니메이션을 한 번 재생하고, 끝나면 스매시(낙하)로 넘어간다.
// 전조 시간은 애니메이션 길이가 정한다. 클립이나 컨트롤러 상태의 Speed를 고치면 전조 시간도 함께 바뀐다. (공격 상태이므로 공격속도 스탯이 재생 속도에 반영된다)
// 피해는 없다.
// 애니메이션: SmashStart(Enemy3-AttackSmashStart 시트, 반복 없음, 컨트롤러 상태 Speed 3)
public class MossbackSmashStartState : MossbackState
{
    public MossbackSmashStartState(Mossback owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        mossback.Movement.SetVelocity(Vector2.zero);

        if (!mossback.IsTargetDead)
        {
            mossback.FaceTarget();
        }

        mossback.Animation.Play(MossbackAnimation.SmashStart);
    }

    public override void LogicUpdate()
    {
        if (mossback.Animation.IsFinished(MossbackAnimation.SmashStart))
        {
            stateMachine.ChangeState(mossback.SmashState);
        }
    }

    public override void PhysicsUpdate()
    {
        mossback.Movement.SetVelocity(Vector2.zero);
    }
}

using UnityEngine;

// 브레스(공격) 전조. 브레스를 뿜기 전에 제자리에 멈춰 날아갈 방향(반대쪽 대기 위치)을 보며 공격 모션을 빠르게 보여 준다. 플레이어가 보고 피할 시간을 준다.
// 대시 전조(BloodWingDashReadyState)와 같은 방식이다.
// AttackReady 애니메이션(ATTACK 시트 8장을 0.5초 안에 3번 도는 빠른 클립, 반복 없음)을 한 번 재생하고, 끝나면 브레스로 넘어간다.
// 전조 시간은 애니메이션 길이가 정한다. 클립을 고치면 전조 시간도 함께 바뀐다. (공격 상태이므로 공격속도 스탯이 재생 속도에 반영된다)
public class BloodWingAttackReadyState : BloodWingState
{
    public BloodWingAttackReadyState(BloodWing owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        Vector2 position = bloodWing.Movement.Position;
        Vector2 destination = bloodWing.GetOppositePerch(position.x);

        bloodWing.Movement.SetVelocity(Vector2.zero);
        bloodWing.Animation.UpdateFacing(destination.x - position.x);
        bloodWing.Animation.Play(BloodWingAnimation.AttackReady);
    }

    public override void LogicUpdate()
    {
        if (bloodWing.Animation.IsFinished(BloodWingAnimation.AttackReady))
        {
            stateMachine.ChangeState(bloodWing.AttackState);
        }
    }

    public override void PhysicsUpdate()
    {
        bloodWing.Movement.SetVelocity(Vector2.zero);
    }
}

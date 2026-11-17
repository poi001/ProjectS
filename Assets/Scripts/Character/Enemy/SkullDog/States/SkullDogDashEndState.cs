// 대시 마무리. 돌진이 끝난 자리에서 마무리 모션을 재생한다. 공중에서 끝났다면 중력으로 곧장 아래로 떨어진다.
// 마무리 모션이 끝나고 바닥에 닿아 있으면 Idle로 쉬어간다. 떨어지는 중이면 마지막 그림을 유지한 채 착지를 기다린다.
// 이 상태에서는 몸에 닿아도 피해가 없다. (피해는 돌진하는 동안에만)
// 애니메이션: DashEnd(Sprite Sheet_9 → 10, 반복 없이 마지막 그림 유지)
public class SkullDogDashEndState : SkullDogState
{
    public SkullDogDashEndState(SkullDog owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        skullDog.Movement.SetVelocityX(0f);
        skullDog.Animation.Play(SkullDogAnimation.DashEnd);
    }

    public override void LogicUpdate()
    {
        if (skullDog.Animation.IsFinished(SkullDogAnimation.DashEnd) && skullDog.Movement.IsGrounded)
        {
            skullDog.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        skullDog.Movement.SetVelocityX(0f);
    }
}

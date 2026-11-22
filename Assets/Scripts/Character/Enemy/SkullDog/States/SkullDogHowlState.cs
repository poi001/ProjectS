// 하울링. 패턴 게이지가 꽉 찬 채로 Idle이면 들어온다. (SkullDog.TryInterruptIdle) 기믹(폭주 런과 분신)의 시작을 알리는 동작이다.
// 들어오는 순간 화면 가로 가운데의 바닥 위로 순간이동하고(SkullDog.TeleportToFieldCenter), 그 자리에서 하울링 모션을 한 번 재생한다. 끝나면 기믹을 시작한다. (SkullDog.BeginGimmick → 폭주 런)
// 하울링하는 동안에는 Ghost 레이어라 플레이어의 공격이 통과하고, 패턴 게이지는 줄지도 차지도 않는다. 몸에 닿아도 피해가 없다.
// 애니메이션: Howl(SkullDog_Howl_00 ~ 11, 반복 없음)
public class SkullDogHowlState : SkullDogState
{
    public SkullDogHowlState(SkullDog owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        skullDog.SetGhost(true);
        skullDog.TeleportToFieldCenter();

        if (!skullDog.IsTargetDead)
        {
            skullDog.FaceTarget();
        }

        skullDog.Animation.Play(SkullDogAnimation.Howl);
    }

    // 하울링이 중간에 끊기는 경우(플레이어 사망 등)에도 원래 레이어로 되돌린다.
    public override void Exit()
    {
        skullDog.SetGhost(false);
    }

    public override void LogicUpdate()
    {
        if (skullDog.Animation.IsFinished(SkullDogAnimation.Howl))
        {
            skullDog.BeginGimmick();
        }
    }

    public override void PhysicsUpdate()
    {
        skullDog.Movement.SetVelocityX(0f);
    }
}

// 기절 후 일어나기. 기절(SoulKnightStunState)이 끝나면 들어온다. Knight의 일어나기와 같다.
// Death 클립을 거꾸로 재생해 쓰러진 모습에서 일어나는 모습으로 돌아가고, 다 재생되면 Idle로 간다.
// 일어나는 동안에는 제자리에 멈춰 있으며 패턴 게이지는 0인 채로 둔다. (기절이 끝날 때 비운다.)
// 도중에 체력이 다해 죽으면 Enemy가 Dead 상태로 바꾸고, 플레이어가 죽으면 Idle로 바꾼다.
public class SoulKnightStunRecoverState : SoulKnightState
{
    public SoulKnightStunRecoverState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        boss.Movement.SetVelocityX(0f);
        boss.Animation.Play(SoulKnightAnimation.StunRecover);
    }

    public override void LogicUpdate()
    {
        if (boss.Animation.IsFinished(SoulKnightAnimation.StunRecover))
        {
            boss.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        boss.Movement.SetVelocityX(0f);
    }
}

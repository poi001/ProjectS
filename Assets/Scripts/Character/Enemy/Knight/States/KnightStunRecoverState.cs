// 기절 후 일어나기. 기절(KnightStunState)이 끝나면 들어온다.
// 기절 애니메이션을 거꾸로 재생해 쓰러진 모습에서 일어나는 모습으로 돌아가고, 다 재생되면 Idle로 간다.
// 일어나는 동안에는 제자리에 멈춰 있으며 패턴 게이지는 0인 채로 둔다. (기절이 끝날 때 비운다.)
// 도중에 체력이 다해 죽으면 Enemy가 Dead 상태로 바꾸고, 플레이어가 죽으면 Idle로 바꾼다.
public class KnightStunRecoverState : KnightState
{
    public KnightStunRecoverState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        knight.Movement.SetVelocityX(0f);
        knight.Animation.Play(KnightAnimation.StunRecover);
    }

    public override void LogicUpdate()
    {
        if (knight.Animation.IsFinished(KnightAnimation.StunRecover))
        {
            knight.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        knight.Movement.SetVelocityX(0f);
    }
}

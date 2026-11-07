// 사망 상태. 사망 애니메이션을 재생하고 멈춘다. 입력은 받지 않는다.
// 사망 애니메이션이 끝나면 한 번 알린다. (CharacterDeathFinishedEvent, KO 연출 종료용)
// 사망 후 처리(재시작 등)는 기획이 정해지면 구현한다.
public class PlayerDeadState : PlayerState
{
    public PlayerDeadState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.Animation.Play(PlayerAnimation.Dead);
    }

    public override void LogicUpdate()
    {
        if (owner.Animation.IsFinished(PlayerAnimation.Dead))
        {
            owner.NotifyDeathFinished();
        }
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(0f);
    }
}

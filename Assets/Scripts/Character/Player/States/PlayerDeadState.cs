// 사망 상태. 체력 시스템이 생기면 진입 조건과 처리 내용을 구현한다. (현재는 껍데기)
public class PlayerDeadState : PlayerState
{
    public PlayerDeadState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(0f);
    }
}

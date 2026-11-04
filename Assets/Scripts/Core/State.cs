// 플레이어/적 공용 상태 베이스. TOwner는 상태가 제어하는 대상(예: Player)이다.
public abstract class State<TOwner>
{
    protected readonly TOwner owner;
    protected readonly StateMachine<TOwner> stateMachine;

    protected State(TOwner owner, StateMachine<TOwner> stateMachine)
    {
        this.owner = owner;
        this.stateMachine = stateMachine;
    }

    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }

    // Update에서 호출: 입력 확인, 상태 전환 판단
    public virtual void LogicUpdate()
    {
    }

    // FixedUpdate에서 호출: 물리 처리
    public virtual void PhysicsUpdate()
    {
    }
}

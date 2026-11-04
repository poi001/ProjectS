// 플레이어/적 공용 유한상태기계. 현재 상태 하나를 보관하고 전환/갱신을 위임한다.
public class StateMachine<TOwner>
{
    public State<TOwner> CurrentState { get; private set; }

    public void Initialize(State<TOwner> startState)
    {
        CurrentState = startState;
        CurrentState.Enter();
    }

    // 같은 상태로 전환해도 Exit → Enter를 다시 호출한다. (예: 공격 종료 직후 재공격 시 처음부터 다시 시작)
    public void ChangeState(State<TOwner> newState)
    {
        CurrentState.Exit();
        CurrentState = newState;
        CurrentState.Enter();
    }

    public void LogicUpdate()
    {
        CurrentState?.LogicUpdate();
    }

    public void PhysicsUpdate()
    {
        CurrentState?.PhysicsUpdate();
    }
}

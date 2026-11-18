// 낙하. 패턴이 공중에서 끝나 Idle로 쉬어야 할 때 들어온다. (Mushroom.ChangeToRest)
// 가로로는 움직이지 않고 중력으로 떨어지며, 땅에 닿으면 Idle로 쉰다. 이 상태에서는 몸에 닿아도 피해가 없다.
// 애니메이션: 따로 재생하지 않고 직전 패턴의 마지막 그림을 유지한다. (발사 뒤라면 Attack_4, 점프공격 뒤라면 Attack_9)
public class MushroomFallState : MushroomState
{
    public MushroomFallState(Mushroom owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        mushroom.Movement.SetVelocityX(0f);
    }

    public override void LogicUpdate()
    {
        if (mushroom.Movement.IsGrounded)
        {
            mushroom.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        mushroom.Movement.SetVelocityX(0f);
    }
}

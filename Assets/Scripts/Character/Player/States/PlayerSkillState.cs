// 스킬 상태. 땅 위에서 스킬 키(스페이스바)를 누르면 스킬 동작(Skill1)을 한 번 재생한다.
// 아직 스킬의 효과(피해, 이펙트 등)는 정해지지 않았다. 지금은 동작이 끝날 때까지 멈춰 있다가 돌아오기만 한다. 스킬은 하나뿐이다.
// (Animator의 Skill1 상태에는 PlayerHero_Skill1 클립이 들어 있다. 클립을 바꾸면 동작 시간도 따라 바뀐다)
// 동작이 끝날 때까지 이동·점프·공격·대시 입력을 받지 않으며, 방향도 바뀌지 않는다. 피격되면 끊긴다.
// 끝나면 쿨타임이 시작되고(Player의 Skill Cooldown) Idle/Run(입력이 있으면 그 동작)으로 돌아간다.
public class PlayerSkillState : PlayerState
{
    public PlayerSkillState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.InputHandler.ConsumeSkillBuffer();
        owner.Movement.Move(0f);
        owner.Animation.Play(PlayerAnimation.Skill1);
    }

    // 피격·사망 등으로 중간에 끊겨도 쿨타임이 시작되게 여기서 처리한다.
    public override void Exit()
    {
        owner.StartSkillCooldown();
    }

    public override void LogicUpdate()
    {
        if (!owner.Animation.IsFinished(PlayerAnimation.Skill1))
        {
            return;
        }

        // 스킬 중 발밑이 사라졌더라도 동작을 끝까지 마친 뒤에 낙하한다.
        if (owner.Movement.IsGrounded)
        {
            ChangeToGroundedState(owner.Animation.GetTimeSinceFinished(PlayerAnimation.Skill1));
        }
        else
        {
            stateMachine.ChangeState(owner.FallState);
        }
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(0f);
    }
}

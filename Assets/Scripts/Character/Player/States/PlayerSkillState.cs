// 스킬 상태. 땅 위에서 스킬 키(스페이스바)를 누르면 장착한 액티브 룬을 시전하고 스킬 동작(Skill1)을 한 번 재생한다. (GDD 3.4, 5.2)
// 스킬은 장착한 액티브 룬 하나뿐이며, 룬을 장착하지 않았으면 이 상태로 들어오지 않는다. (Player.CanSkill)
// 시작할 때 그 룬의 기력을 소모하고(Player.ConsumeSkillStamina) 룬을 시전한다. (Player.CastSkill) 효과는 룬(ActiveRune.OnCast)이 정한다.
// (Animator의 Skill1 상태에는 PlayerHero_Skill1 클립이 들어 있다. 클립을 바꾸면 동작 시간도 따라 바뀐다)
// 동작이 끝날 때까지 이동·점프·공격·대시 입력을 받지 않으며, 방향도 바뀌지 않는다. 피격되면 끊긴다. (이미 시전한 효과는 룬이 따로 끝내지 않는 한 남는다)
// 끝나면 그 룬의 쿨타임이 시작되고(Player.StartSkillCooldown) Idle/Run(입력이 있으면 그 동작)으로 돌아간다.
public class PlayerSkillState : PlayerState
{
    public PlayerSkillState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        owner.InputHandler.ConsumeSkillBuffer();
        owner.ConsumeSkillStamina();
        owner.Movement.Move(0f);
        owner.Animation.Play(PlayerAnimation.Skill1);
        owner.CastSkill();
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

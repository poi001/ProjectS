// 스킬. 제자리에서 몸을 일으켜 크게 내려친다. 내려치는 구간에 앞쪽 넓은 범위(궤적)로 판정이 나간다. (StoneGolem의 '스킬' 항목)
// 애니메이션: Skill(gollux_attack_B 0 ~ 18, 한 번 재생)
public class StoneGolemSkillState : StoneGolemStrikeStateBase
{
    public StoneGolemSkillState(StoneGolem owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected override int AnimationHash => StoneGolemAnimation.Skill;
    protected override StoneGolemStrike Strike => golem.Skill;
}

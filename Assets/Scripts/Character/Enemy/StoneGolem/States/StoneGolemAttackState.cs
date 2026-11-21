// 공격. 제자리에서 주먹으로 바닥을 친다. 주먹이 닿는 구간에 앞쪽 좁은 범위로 판정이 나간다. (StoneGolem의 '공격' 항목)
// 애니메이션: Attack(gollux_attack_A 0 ~ 16, 한 번 재생)
public class StoneGolemAttackState : StoneGolemStrikeStateBase
{
    public StoneGolemAttackState(StoneGolem owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected override int AnimationHash => StoneGolemAnimation.Attack;
    protected override StoneGolemStrike Strike => golem.Attack;
}

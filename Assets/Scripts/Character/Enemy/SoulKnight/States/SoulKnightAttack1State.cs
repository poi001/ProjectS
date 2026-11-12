// 공격 1타. 공격 4타 묶음의 첫 공격이며, 플레이어가 공격 사거리 안일 때만 이 공격부터 시작한다. (Enemy_GDD 6.9)
// 시작할 때 플레이어 쪽으로 돌아서고, Knight처럼 살짝 전진하며 휘두른다. 끝나면 딜레이 없이 2타로 이어진다.
public class SoulKnightAttack1State : SoulKnightSwingStateBase
{
    public SoulKnightAttack1State(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected override int AnimationHash => SoulKnightAnimation.Attack1;
    protected override SoulKnightSwing Swing => boss.Attack1Swing;
    protected override bool FacesTargetOnEnter => true;

    protected override void ChangeToNextState()
    {
        stateMachine.ChangeState(boss.Attack2State);
    }
}

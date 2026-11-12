// 공격 2타. 1타가 끝나면 딜레이 없이 들어온다. (Enemy_GDD 6.9)
// 방향을 다시 정하지 않고 1타와 같은 방향으로 살짝 전진하며 휘두른다. 끝나면 딜레이 없이 찌르기(Thrust)로 이어진다.
public class SoulKnightAttack2State : SoulKnightSwingStateBase
{
    public SoulKnightAttack2State(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected override int AnimationHash => SoulKnightAnimation.Attack2;
    protected override SoulKnightSwing Swing => boss.Attack2Swing;
    protected override bool FacesTargetOnEnter => false;

    protected override void ChangeToNextState()
    {
        stateMachine.ChangeState(boss.ThrustState);
    }
}

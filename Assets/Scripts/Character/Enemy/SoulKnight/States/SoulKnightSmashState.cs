// 공격 4타(내려치기). 찌르기(Thrust)가 끝나면 딜레이 없이 들어온다. (Enemy_GDD 6.9)
// 시작할 때 플레이어 쪽으로 돌아서서 제자리에서 휘두른다. 전진하지 않는다. 끝나면 Idle로 쉰다.
public class SoulKnightSmashState : SoulKnightSwingStateBase
{
    public SoulKnightSmashState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected override int AnimationHash => SoulKnightAnimation.Smash;
    protected override SoulKnightSwing Swing => boss.SmashSwing;
    protected override bool FacesTargetOnEnter => true;
    protected override bool CanStep => false;

    protected override void ChangeToNextState()
    {
        boss.ChangeToRest();
    }
}

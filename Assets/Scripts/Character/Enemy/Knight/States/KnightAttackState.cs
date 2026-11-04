// 공격 1회. 플레이어 쪽으로 살짝 이동하면서 한 번 휘두른다. (GDD 6.6)
public class KnightAttackState : KnightAttackBaseState
{
    public KnightAttackState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected override int AnimationHash => KnightAnimation.Attack;
    protected override int StepCount => 1;

    protected override float GetStepStartTime(int stepIndex)
    {
        return 0f;
    }
}

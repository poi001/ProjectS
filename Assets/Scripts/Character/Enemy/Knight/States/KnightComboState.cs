// 공격 2회. 두 번 휘두르는 콤보이며, 휘두를 때마다 플레이어 쪽으로 살짝씩 이동한다. (GDD 6.6)
public class KnightComboState : KnightAttackBaseState
{
    public KnightComboState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected override int AnimationHash => KnightAnimation.AttackCombo;
    protected override int StepCount => 2;

    protected override float GetStepStartTime(int stepIndex)
    {
        return stepIndex == 0 ? 0f : knight.ComboSecondStepTime;
    }
}

// 공격 2회. 두 번 휘두르는 콤보이며, 휘두를 때마다 살짝씩 전진한다. (GDD 6.6)
// 방향은 시작할 때 플레이어 쪽으로 한 번만 정하고, 두 번 모두 휘두를 때까지 바꾸지 않는다.
public class KnightComboState : KnightAttackBaseState
{
    public KnightComboState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    protected override int AnimationHash => KnightAnimation.AttackCombo;
    protected override KnightSwing[] Swings => knight.ComboSwings;
}

// 구르기. 공격 1회/2회가 끝난 뒤 랜덤으로 딜레이 없이 시전되며, 플레이어 쪽으로 정해진 속도로 구른다.
// 플레이어와는 서로 관통하므로 플레이어를 지나쳐 반대편까지 구를 수 있다. (GDD 6.7)
// 구르기가 끝나면 딜레이 없이 바로 공격 1회 또는 공격 2회를 시전한다. (GDD 6.6)
// 이 공격이 끝난 뒤에는 구르기를 하지 않고 Idle로 간다. (구르기가 연달아 이어지지 않는다.)
public class KnightRollState : KnightState
{
    private float _direction;

    public KnightRollState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _direction = knight.DirectionToTarget;
        knight.FaceTarget();
        knight.Animation.Play(KnightAnimation.Roll);
    }

    public override void Exit()
    {
        knight.Movement.SetVelocityX(0f);
    }

    public override void LogicUpdate()
    {
        if (knight.Animation.IsFinished(KnightAnimation.Roll))
        {
            knight.ChangeToAttackPattern(false);
        }
    }

    public override void PhysicsUpdate()
    {
        SetPatternVelocityX(_direction * knight.RollSpeed);
    }
}

// 적 상태 공통 베이스. 여러 상태가 함께 쓰는 판단 로직을 모아둔다.
public abstract class EnemyState : State<Enemy>
{
    protected EnemyState(Enemy owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 패턴 이동용 가로 속도를 정한다. 그 방향이 지형에 막혀 있으면 밀지 않는다.
    // 막힌 쪽으로 계속 밀면 마찰 때문에 벽/발판 옆면에 붙어 떨어지지 않으므로 이를 막는다.
    protected void SetPatternVelocityX(float velocityX)
    {
        if (velocityX != 0f && owner.Movement.IsBlocked(velocityX))
        {
            velocityX = 0f;
        }

        owner.Movement.SetVelocityX(velocityX);
    }
}

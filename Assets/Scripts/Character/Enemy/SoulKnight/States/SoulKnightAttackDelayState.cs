// 공격 전 딜레이. 각성 후 패턴으로 공격 4타가 선택되면, 첫 공격(1타 또는 찌르기) 전에 잠깐 멈춰 공격을 예고한다. (Enemy_GDD 6.9)
// Knight가 공격 1회/2회 전에 준비 자세로 잠깐 멈추는 것과 같은 역할이다. 이후 공격들은 딜레이 없이 이어진다.
// 제자리에 멈춰 플레이어 쪽을 바라보며 AttackDelay 애니메이션(crouch_idle_0, 0.3초)을 한 번 재생하고, 끝나면 정해 둔 첫 공격으로 넘어간다.
// 딜레이 시간은 애니메이션 길이가 정한다. 클립을 고치면 딜레이 시간도 함께 바뀐다.
// 첫 공격은 들어오기 전에 SetNextState로 정한다. (사거리 안: 1타, 밖: 찌르기)
public class SoulKnightAttackDelayState : SoulKnightState
{
    private State<Enemy> _nextState;

    public SoulKnightAttackDelayState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 딜레이가 끝난 뒤 시작할 첫 공격 상태를 정한다. 이 상태로 바꾸기 전에 호출한다.
    public void SetNextState(State<Enemy> nextState)
    {
        _nextState = nextState;
    }

    public override void Enter()
    {
        boss.Movement.SetVelocityX(0f);

        if (!boss.IsTargetDead)
        {
            boss.FaceTarget();
        }

        boss.Animation.Play(SoulKnightAnimation.AttackDelay);
    }

    public override void LogicUpdate()
    {
        if (!boss.Animation.IsFinished(SoulKnightAnimation.AttackDelay))
        {
            return;
        }

        if (_nextState == null)
        {
            boss.ChangeToRest();
            return;
        }

        stateMachine.ChangeState(_nextState);
    }

    public override void PhysicsUpdate()
    {
        boss.Movement.SetVelocityX(0f);
    }
}

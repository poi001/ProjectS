using UnityEngine;

// 브레스(공격). 지금 있는 자리에서 반대쪽 대기 위치까지 같은 높이로 일정한 속도로(BreathDuration 동안) 날아가며, 입에서 날아가는 쪽 아래 대각선으로 화염을 뿜는다.
// 도착하면 화염을 끄고 화면 안쪽(날아온 쪽)을 돌아본 뒤 Idle로 쉰다.
// 화염의 길이·판정·그림은 BloodWing.UpdateBreath가 매 물리 프레임 맞춘다. (바닥·벽에 닿는 곳까지 뻗는 긴 직사각형)
// 화염은 앞쪽 아래로 기울어 있으므로 보스 바로 아래(화염 뒤쪽)가 안전하다.
// 애니메이션: Attack(ATTACK_3 한 장 고정)
public class BloodWingAttackState : BloodWingState
{
    private readonly BloodWingLinearMove _move = new BloodWingLinearMove();
    // 날아가는 가로 방향 (오른쪽 1, 왼쪽 -1)
    private float _direction;

    public BloodWingAttackState(BloodWing owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        Vector2 start = bloodWing.Movement.Position;
        Vector2 end = bloodWing.GetOppositePerch(start.x);
        _direction = end.x >= start.x ? 1f : -1f;

        _move.Begin(start, end, bloodWing.BreathDuration);
        bloodWing.Animation.UpdateFacing(_direction);
        bloodWing.Animation.Play(BloodWingAnimation.Attack);
        bloodWing.SetBreathFlameVisible(true);
        bloodWing.UpdateBreath(_direction);
    }

    // 브레스가 중간에 끊기는 경우(플레이어 사망, BloodWing 사망 등)에도 그 자리에 멈추고 화염을 끈다.
    public override void Exit()
    {
        bloodWing.Movement.SetVelocity(Vector2.zero);
        bloodWing.SetBreathFlameVisible(false);
    }

    public override void PhysicsUpdate()
    {
        bloodWing.UpdateBreath(_direction);

        if (!_move.Step(bloodWing.Movement))
        {
            return;
        }

        // 반대쪽 대기 위치에 도착: 화면 안쪽을 돌아본 뒤 Idle로 쉰다. (화염은 Exit에서 끈다)
        bloodWing.FaceFieldCenter();
        bloodWing.ChangeToIdle(bloodWing.PatternInterval);
    }
}

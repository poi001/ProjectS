using UnityEngine;

// 점프. 플레이어 머리 위로 뛰어오른 뒤, 최고점에서 딜레이 없이 점프공격(수직 내려찍기)으로 이어진다. (Enemy_GDD 6장)
// 목표 지점은 점프를 시작하는 순간의 플레이어 위치 위, SoulKnight.JumpHeight만큼 높은 곳이다. 시작한 뒤에는 플레이어를 따라가지 않는다.
// 목표 x는 몸이 벽과 겹치지 않는 범위로 제한하므로 플레이어가 벽에 붙어 있어도 벽을 넘거나 겹치지 않는다.
// 점프 궤적은 목표 지점에 최고점으로 도착하도록 계산한다. (CharacterMovement.CalculateJumpVelocityTo, 가로 속도 제한 없음)
// 가로 이동은 목표 x를 지나치지 않도록 마지막 물리 프레임에 남은 거리만큼만 움직인다.
public class SoulKnightJumpState : SoulKnightState
{
    // 최고점에 닿지 못하는 예외 상황에서 점프공격으로 넘어가는 시간 (단위: 초)
    private const float MaxJumpDuration = 3f;

    private float _targetX;
    // 가로 속도의 크기 (단위: 월드 거리/초)
    private float _speedX;
    private float _enterTime;

    public SoulKnightJumpState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        _targetX = boss.ClampInsideWalls(boss.Target.transform.position.x);

        float distanceX = Mathf.Abs(_targetX - boss.Movement.Position.x);
        Vector2 jumpVelocity = boss.Movement.CalculateJumpVelocityTo(distanceX, boss.JumpHeight, 0f);
        _speedX = jumpVelocity.x;

        boss.FaceTarget();
        boss.Movement.Jump(jumpVelocity.y);
        boss.Animation.Play(SoulKnightAnimation.Jump);
    }

    public override void Exit()
    {
        boss.Movement.SetVelocityX(0f);
    }

    public override void PhysicsUpdate()
    {
        bool isTimeOver = Time.time - _enterTime >= MaxJumpDuration;

        // 올라가는 속도가 0 이하가 되면 최고점이다. (천장 등에 막혀 멈춘 경우도 같다)
        if (boss.Movement.Velocity.y <= 0f || isTimeOver)
        {
            stateMachine.ChangeState(boss.JumpAttackState);
            return;
        }

        float remainingDistance = _targetX - boss.Movement.Position.x;
        SetPatternVelocityX(Mathf.Clamp(remainingDistance / Time.fixedDeltaTime, -_speedX, _speedX));
    }
}

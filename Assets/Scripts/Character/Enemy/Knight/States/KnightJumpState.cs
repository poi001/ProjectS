using UnityEngine;

// 점프. 플레이어가 있는 위치를 향해 뛰어올라 착지한다. 착지하면 Idle 없이 바로 공격 1회 또는 공격 2회를 시전한다. (GDD 6.6)
// 가로 속도는 같은 높이에 착지한다고 보고, 체공 시간 동안 시작 시점의 플레이어 위치에 닿도록 계산한다.
// Jump 애니메이션이 끝나면 착지할 때까지 마지막 프레임을 유지한다.
public class KnightJumpState : KnightState
{
    private float _velocityX;

    public KnightJumpState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        float distanceX = knight.Target.transform.position.x - knight.transform.position.x;
        float airTime = knight.Movement.JumpApexTime * 2f;
        _velocityX = Mathf.Clamp(distanceX / airTime, -knight.JumpMaxSpeedX, knight.JumpMaxSpeedX);

        knight.FaceTarget();
        knight.Movement.Jump();
        knight.Animation.Play(KnightAnimation.Jump);
    }

    public override void Exit()
    {
        knight.Movement.SetVelocityX(0f);
    }

    public override void LogicUpdate()
    {
        if (knight.Movement.IsGrounded)
        {
            knight.ChangeToAttackPattern(true);
        }
    }

    public override void PhysicsUpdate()
    {
        SetPatternVelocityX(_velocityX);
    }
}

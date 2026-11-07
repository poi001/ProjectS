using UnityEngine;

// 점프. 플레이어가 있는 위치를 향해 뛰어올라 착지한다. 착지 모션이 끝나면 Idle 없이 바로 공격 1회 또는 공격 2회를 시전한다. (Enemy_GDD 5.2)
// 가로 속도는 같은 높이에 착지한다고 보고, 체공 시간 동안 시작 시점의 플레이어 위치에 닿도록 계산한다.
// 세 단계로 나뉜다. 애니메이션: Jump(뛰어오름, 한 번) → Fall(공중, 반복) → Land(착지, 한 번)
// - 뛰어오름: Jump 애니메이션이 끝날 때까지
// - 공중: Jump가 끝난 뒤 바닥에 닿을 때까지 Fall을 반복한다.
// - 착지: 바닥에 닿으면 제자리에 멈춰 Land를 재생하고, 끝나면 공격으로 넘어간다.
public class KnightJumpState : KnightState
{
    private float _velocityX;
    private bool _isFalling;
    private bool _isLanding;

    public KnightJumpState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        knight.PatternGauge.Add(knight.JumpGaugeGain);
        _isFalling = false;
        _isLanding = false;

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
        if (_isLanding)
        {
            if (knight.Animation.IsFinished(KnightAnimation.Land))
            {
                knight.ChangeToAttackPattern(true);
            }

            return;
        }

        if (knight.Movement.IsGrounded)
        {
            StartLanding();
            return;
        }

        if (!_isFalling && knight.Animation.IsFinished(KnightAnimation.Jump))
        {
            _isFalling = true;
            knight.Animation.Play(KnightAnimation.Fall);
        }
    }

    public override void PhysicsUpdate()
    {
        if (_isLanding)
        {
            knight.Movement.SetVelocityX(0f);
            return;
        }

        SetPatternVelocityX(_velocityX);
    }

    // 바닥에 닿았을 때: 제자리에 멈추고 착지 애니메이션을 재생한다.
    private void StartLanding()
    {
        _isLanding = true;
        knight.Movement.SetVelocityX(0f);
        knight.Animation.Play(KnightAnimation.Land);
    }
}

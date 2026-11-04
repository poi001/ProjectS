using UnityEngine;

// 벽타기. 가장 가까운 벽으로 점프해 닿으면 매달리고, 정해진 시간이 지나면 슬라이딩으로 자동으로 이어진다. (GDD 6.6)
// 가로 속도는 점프 최고점에서 벽에 닿도록 계산하되, 최대 속도를 넘으면 낙하 중에 닿는다.
// 좌우에 벽이 없으면 점프 패턴으로 대신하고, 벽에 닿지 못하고 착지하면 패턴을 끝낸다.
public class KnightWallClimbState : KnightState
{
    private bool _hasWall;
    private bool _isHanging;
    // 벽이 있는 방향 (오른쪽 1, 왼쪽 -1)
    private float _wallDirection;
    private float _velocityX;
    private float _hangStartTime;

    public KnightWallClimbState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _isHanging = false;
        _hasWall = knight.Movement.TryFindNearestWall(out _wallDirection, out float wallDistance);

        if (!_hasWall)
        {
            return;
        }

        float speedX = Mathf.Min(wallDistance / knight.Movement.JumpApexTime, knight.JumpMaxSpeedX);
        _velocityX = _wallDirection * speedX;

        knight.Animation.UpdateFacing(_wallDirection);
        knight.Movement.Jump();
        knight.Animation.Play(KnightAnimation.Jump);
    }

    public override void Exit()
    {
        knight.Movement.SetGravityEnabled(true);
        knight.Movement.SetVelocityX(0f);
    }

    public override void LogicUpdate()
    {
        if (!_hasWall)
        {
            stateMachine.ChangeState(knight.JumpState);
            return;
        }

        if (_isHanging)
        {
            if (Time.time - _hangStartTime >= knight.WallHangDuration)
            {
                stateMachine.ChangeState(knight.SlideState);
            }

            return;
        }

        if (knight.Movement.IsTouchingWall(_wallDirection))
        {
            StartHanging();
            return;
        }

        if (knight.Movement.IsGrounded)
        {
            knight.ChangeToIdleOrNextPattern();
        }
    }

    public override void PhysicsUpdate()
    {
        if (!_hasWall)
        {
            return;
        }

        if (_isHanging)
        {
            knight.Movement.SetVelocity(Vector2.zero);
            return;
        }

        SetPatternVelocityX(_velocityX);
    }

    private void StartHanging()
    {
        _isHanging = true;
        _hangStartTime = Time.time;
        knight.Movement.SetGravityEnabled(false);
        knight.Movement.SetVelocity(Vector2.zero);
        knight.Animation.Play(KnightAnimation.WallHang);
    }
}

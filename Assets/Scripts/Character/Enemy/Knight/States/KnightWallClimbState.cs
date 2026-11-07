using UnityEngine;

// 벽타기. 가장 가까운 벽의 고정된 높이(Knight.WallHangHeight)에 매달리는 지점을 향해 점프하고, 닿으면 매달린다.
// 정해진 시간이 지나면 슬라이딩(대각선 낙하)으로 자동으로 이어진다. (GDD 6.6)
// 점프 궤적은 매달리는 지점을 정확히 지나가도록 계산한다. (CharacterMovement.CalculateJumpVelocityTo)
// 기본은 최고점에서 벽에 닿고, 벽이 멀어 가로 속도가 최대 속도를 넘으면 내려오면서 닿는다.
// 항상 같은 높이에 매달리므로, 바닥에 가까워 슬라이딩이 시작하자마자 접지로 판정되는 일이 없다.
// 닿은 뒤 남은 작은 높이 오차는 벽을 타는 보정으로 맞춘다. 그 순간의 플레이어 위치를 포착해 슬라이딩 낙하 지점으로 정한다.
// 좌우에 벽이 없으면 점프 패턴으로 대신하고, 벽에 닿지 못하고 착지하면 패턴을 끝낸다.
public class KnightWallClimbState : KnightState
{
    // 매달리는 높이에 이 거리 안으로 들어오면 도착한 것으로 본다. (단위: 월드 거리)
    private const float HangHeightTolerance = 0.02f;
    // 벽 앞에서 멈출 때 콜라이더 가장자리와 벽 사이에 남기는 간격. 벽 접촉 판정 거리보다 작아야 한다. (단위: 월드 거리)
    private const float HangWallGap = 0.05f;
    // 벽에 닿았을 때 매달리는 높이와 이 거리 안이면 매달리기를 시작한다. 남은 오차는 벽을 타는 보정으로 맞춘다. (단위: 월드 거리)
    private const float NearHangHeight = 0.1f;

    private bool _hasWall;
    // 벽에 닿아 매달리는 높이까지 벽을 타는 중인지
    private bool _isClimbing;
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
        _isClimbing = false;
        _isHanging = false;
        _hasWall = knight.Movement.TryFindNearestWall(out _wallDirection, out float wallDistance);

        if (!_hasWall)
        {
            return;
        }

        // 매달리는 지점(벽 앞, 고정 높이)을 정확히 지나가는 점프 속도를 구한다.
        float travelDistance = Mathf.Max(0f, wallDistance - HangWallGap);
        float heightGain = knight.WallHangHeight - knight.Movement.Position.y;
        Vector2 jumpVelocity = knight.Movement.CalculateJumpVelocityTo(travelDistance, heightGain, knight.JumpMaxSpeedX);
        _velocityX = _wallDirection * jumpVelocity.x;

        knight.Animation.UpdateFacing(_wallDirection);
        knight.Movement.Jump(jumpVelocity.y);
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

        if (_isClimbing)
        {
            return;
        }

        if (CanStartClimbing())
        {
            StartClimbing();
            return;
        }

        if (knight.Movement.IsGrounded)
        {
            knight.ChangeToRest();
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

        if (_isClimbing)
        {
            ClimbToHangHeight();
            return;
        }

        SetPatternVelocityX(_velocityX);
    }

    // 벽에 닿았고, 매달리는 높이 근처이거나 내려오는 중이면 매달리기를 시작한다.
    // 벽 바로 옆에서 시작해 올라가는 중에 닿은 경우에는, 매달리는 높이에 가까워질 때까지 점프를 이어간다.
    private bool CanStartClimbing()
    {
        if (!knight.Movement.IsTouchingWall(_wallDirection))
        {
            return false;
        }

        float heightGap = Mathf.Abs(knight.WallHangHeight - knight.Movement.Position.y);
        return heightGap <= NearHangHeight || knight.Movement.Velocity.y <= 0f;
    }

    // 벽에 닿았을 때: 점프를 멈추고 매달리는 높이와의 남은 오차만큼 벽을 타며 보정하기 시작한다.
    private void StartClimbing()
    {
        _isClimbing = true;
        knight.Movement.SetGravityEnabled(false);
        knight.Movement.SetVelocity(Vector2.zero);
        knight.Animation.Play(KnightAnimation.WallHang);
    }

    // 매달리는 높이를 향해 세로로 이동한다. 마지막 한 걸음은 남은 거리만큼만 가서 높이를 지나치지 않게 한다.
    private void ClimbToHangHeight()
    {
        float deltaY = knight.WallHangHeight - knight.Movement.Position.y;

        if (Mathf.Abs(deltaY) <= HangHeightTolerance)
        {
            StartHanging();
            return;
        }

        float velocityY = Mathf.Clamp(deltaY / Time.fixedDeltaTime, -knight.WallClimbSpeed, knight.WallClimbSpeed);
        knight.Movement.SetVelocity(new Vector2(0f, velocityY));
    }

    // 매달리는 높이에 도착했을 때: 멈추고, 그 순간의 플레이어 위치로 낙하 지점을 정한다.
    private void StartHanging()
    {
        _isClimbing = false;
        _isHanging = true;
        _hangStartTime = Time.time;
        knight.Movement.SetVelocity(Vector2.zero);
        knight.CaptureSlideTarget();
    }
}

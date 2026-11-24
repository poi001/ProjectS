using UnityEngine;

// 벽타기 상태. 공중에서 벽 쪽 이동 키를 누르고 있는 동안 벽에 붙어 일정한 속도로 미끄러져 내려간다. (중력 없음)
// 원본 스프라이트는 왼쪽 벽에 붙은 모습이므로 벽 반대쪽을 바라보게 해서 오른쪽 벽이면 좌우를 뒤집는다.
// 시작할 때 공중 대시를 다시 채운다. (공중 점프는 땅에 닿아야만 채워진다)
// 기력: 시작할 때 시작 비용을 한 번 소모하고, 매달려 있는 동안 시간에 따라 연속해서 소모한다. 기력이 바닥나면 벽에서 떨어진다. (Player의 기력 항목, GDD 4.5)
// 점프 → 벽 점프(벽 점프를 얻었을 때), 대시 → 벽 반대쪽으로 대시(대시를 얻었을 때), 벽 쪽 키를 떼거나 벽이 끝나거나 기력이 바닥나면 → Fall, 땅에 닿으면 → Idle/Run.
// 벽에 붙어 있는 동안 공격 입력은 받지 않는다. (벽에서 떨어진 뒤 버퍼 시간 안이면 점프 공격으로 이어진다)
public class PlayerWallSlideState : PlayerState
{
    // 붙어 있는 벽의 방향 (오른쪽 1, 왼쪽 -1)
    private float _wallDirection = 1f;

    public PlayerWallSlideState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 다음 Enter에서 붙을 벽의 방향을 정한다. (오른쪽 양수, 왼쪽 음수)
    public void SetWallDirection(float direction)
    {
        _wallDirection = direction >= 0f ? 1f : -1f;
    }

    public override void Enter()
    {
        owner.ConsumeWallSlideStartStamina();
        owner.RefillAirDash();
        // 벽과 조금 떨어진 채로 붙었을 수 있으므로 몸을 벽에 맞닿게 옮긴다.
        owner.Movement.SnapToWall(_wallDirection);
        owner.Animation.UpdateFacing(-_wallDirection);
        // 미끄러지는 속도를 일정하게 유지하도록 중력을 끈다. (켜 두면 매 물리 스텝 중력만큼 더 빨라진다)
        owner.Movement.SetGravityEnabled(false);
        // 그림이 칸 가운데에서 벗어나 있어 벽에서 떠 보이므로 벽 쪽(바라보는 방향의 뒤)으로 옮긴다.
        owner.Animation.SetSpriteOffsetX(-owner.WallSlideSpriteOffset);
        owner.Animation.Play(PlayerAnimation.WallSlide);
        owner.Animation.PlayWallSlideDust();
    }

    public override void Exit()
    {
        owner.Animation.StopWallSlideDust();
        owner.Movement.SetGravityEnabled(true);
        owner.Animation.SetSpriteOffsetX(0f);
    }

    public override void LogicUpdate()
    {
        if (owner.Movement.IsGrounded)
        {
            ChangeToGroundedState();
            return;
        }

        // 벽 점프를 얻지 못했으면(Knight 처치) 점프 입력이 있어도 벽에 붙은 채로 둔다.
        if (owner.InputHandler.HasBufferedJump && owner.HasAbility(PlayerAbility.WallJump))
        {
            owner.WallJumpState.SetDirection(-_wallDirection);
            stateMachine.ChangeState(owner.WallJumpState);
            return;
        }

        if (TryChangeToDashState(-_wallDirection))
        {
            return;
        }

        if (!IsHoldingTowardWall() || !owner.Movement.IsTouchingWall(_wallDirection))
        {
            stateMachine.ChangeState(owner.FallState);
            return;
        }

        // 매달려 있는 시간만큼 기력이 닳는다. 바닥나면 벽에서 떨어진다.
        if (!owner.DrainWallSlideStamina(Time.deltaTime))
        {
            stateMachine.ChangeState(owner.FallState);
        }
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.SetVelocity(new Vector2(0f, -owner.WallSlideSpeed));
    }

    private bool IsHoldingTowardWall()
    {
        return IsMoving() && Mathf.Sign(GetMoveInput()) == _wallDirection;
    }
}

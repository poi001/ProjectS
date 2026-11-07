using UnityEngine;

// 벽타기 상태. 공중에서 벽 쪽 이동 키를 누르고 있는 동안 벽에 붙어 일정한 속도로 미끄러져 내려간다. (중력 없음)
// 원본 스프라이트는 왼쪽 벽에 붙은 모습이므로 벽 반대쪽을 바라보게 해서 오른쪽 벽이면 좌우를 뒤집는다.
// 시작할 때 공중 점프와 공중 대시를 다시 채운다.
// 점프 → 벽 점프, 대시 → 벽 반대쪽으로 대시, 벽 쪽 키를 떼거나 벽이 끝나면 → Fall, 땅에 닿으면 → Idle/Run.
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
        owner.RefillAirActions();
        owner.Animation.UpdateFacing(-_wallDirection);
        // 미끄러지는 속도를 일정하게 유지하도록 중력을 끈다. (켜 두면 매 물리 스텝 중력만큼 더 빨라진다)
        owner.Movement.SetGravityEnabled(false);
        // 그림이 칸 가운데에서 벗어나 있어 벽에서 떠 보이므로 벽 쪽(바라보는 방향의 뒤)으로 옮긴다.
        owner.Animation.SetSpriteOffsetX(-owner.WallSlideSpriteOffset);
        owner.Animation.Play(PlayerAnimation.WallSlide);
    }

    public override void Exit()
    {
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

        if (owner.InputHandler.HasBufferedJump)
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

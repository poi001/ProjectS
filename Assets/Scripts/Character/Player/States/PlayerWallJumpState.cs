using UnityEngine;

// 벽 점프 상태. 벽 반대쪽으로 튀어 나가며 WallJump 애니메이션을 재생한다.
// 튀어 나가는 방향은 상태에 들어가기 전에 SetDirection으로 정한다. (벽타기 상태가 벽 반대쪽으로 정한다)
// 직후 잠깐(입력 잠금 시간) 좌우 입력을 무시해 튀어 나가는 속도를 유지하고, 그 뒤로는 점프처럼 공중에서 움직일 수 있다.
// 점프 상태와 같이 대시, 공중 점프, 점프 공격, 벽타기로 전환할 수 있고, 애니메이션이 끝나면 Fall 또는 Idle/Run으로 전환한다.
public class PlayerWallJumpState : PlayerState
{
    // 튀어 나가는 방향 (오른쪽 1, 왼쪽 -1)
    private float _direction = 1f;
    private float _inputLockEndTime;

    private bool IsInputLocked => Time.time < _inputLockEndTime;

    public PlayerWallJumpState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
    }

    // 다음 Enter에서 튀어 나갈 방향을 정한다. (오른쪽 양수, 왼쪽 음수)
    public void SetDirection(float direction)
    {
        _direction = direction >= 0f ? 1f : -1f;
    }

    public override void Enter()
    {
        owner.InputHandler.ConsumeJumpBuffer();
        owner.Animation.UpdateFacing(_direction);
        owner.Movement.Jump(owner.Movement.GetJumpVelocity(owner.WallJumpHeightRatio));
        owner.Movement.SetVelocityX(_direction * owner.WallJumpSpeedX);
        _inputLockEndTime = Time.time + owner.WallJumpInputLockTime;
        owner.Animation.Play(PlayerAnimation.WallJump);
    }

    public override void LogicUpdate()
    {
        if (!IsInputLocked)
        {
            UpdateFacing();
        }

        if (TryChangeFromAirborneState())
        {
            return;
        }

        if (!owner.Animation.IsFinished(PlayerAnimation.WallJump))
        {
            return;
        }

        if (owner.Movement.IsGrounded)
        {
            ChangeToGroundedState();
        }
        else
        {
            stateMachine.ChangeState(owner.FallState);
        }
    }

    public override void PhysicsUpdate()
    {
        // 입력 잠금 동안에는 튀어 나가는 가로 속도를 그대로 둔다.
        if (IsInputLocked)
        {
            return;
        }

        owner.Movement.Move(GetMoveInput());
    }
}

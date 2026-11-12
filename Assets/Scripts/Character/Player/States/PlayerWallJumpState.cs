using UnityEngine;

// 벽 점프 상태. 벽 반대쪽으로 수평으로 튀어 나가며 WallJump 애니메이션을 재생한다. (위로 오르지 않는다)
// 튀어 나가는 방향은 상태에 들어가기 전에 SetDirection으로 정한다. (벽타기 상태가 벽 반대쪽으로 정한다)
// 직후 잠깐(입력 잠금 시간) 좌우 입력을 무시해 튀어 나가는 속도를 유지하고, 그 뒤로는 점프처럼 공중에서 움직일 수 있다.
// 점프 상태와 같이 대시, 공중 점프, 점프 공격, 벽타기로 전환할 수 있고, 애니메이션이 끝나면 Fall 또는 Idle/Run으로 전환한다.
// 입력 잠금 동안에는 중력을 꺼서 높이를 유지하고, 잠금이 끝나면 중력을 되돌린다. 잠금 시간이 곧 수평 유지 시간이다.
// 위로 오르지 않으므로 입력 잠금 동안에는 벽타기로 전환하지 않는다. (벽 쪽 키를 누른 채 점프해도 바로 같은 벽에 다시 붙지 않게 한다)
public class PlayerWallJumpState : PlayerState
{
    // 튀어 나가는 방향 (오른쪽 1, 왼쪽 -1)
    private float _direction = 1f;
    private float _inputLockEndTime;
    private bool _isGravityDisabled;

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
        // 세로 속도를 0으로 만들고 입력 잠금 동안 중력을 꺼서 높이를 유지한 채 수평으로 튀어 나간다.
        // (Jump는 접지 판정 무시 시간 등 점프 처리를 함께 한다)
        owner.Movement.Jump(0f);
        owner.Movement.SetGravityEnabled(false);
        _isGravityDisabled = true;
        owner.Movement.SetVelocityX(_direction * owner.WallJumpSpeedX);
        // 떠난 벽(튀어 나가는 방향의 반대쪽)으로는 한동안 입력이 먹지 않게 한다.
        owner.BlockWallDirection(-_direction);
        _inputLockEndTime = Time.time + owner.WallJumpInputLockTime;
        owner.Animation.Play(PlayerAnimation.WallJump);
    }

    public override void Exit()
    {
        EnableGravity();
    }

    public override void LogicUpdate()
    {
        if (!IsInputLocked)
        {
            // 입력 잠금이 끝나면 중력을 되돌려 떨어지기 시작한다.
            EnableGravity();
            UpdateFacing();
        }

        if (TryChangeToDashState()
            || TryChangeToAirJumpState()
            || TryChangeToJumpAttackState()
            || (!IsInputLocked && TryChangeToWallSlideState()))
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

    private void EnableGravity()
    {
        if (!_isGravityDisabled)
        {
            return;
        }

        owner.Movement.SetGravityEnabled(true);
        _isGravityDisabled = false;
    }
}

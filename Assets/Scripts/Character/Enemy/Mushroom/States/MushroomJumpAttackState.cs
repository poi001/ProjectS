using UnityEngine;

// 점프공격(공중공격). 준비 동작 뒤, 준비 도중에 기억해 둔 플레이어 쪽으로 일정한 속도(JumpAttackSpeed)로 돌진한다.
//  - 준비: JumpAttackReadyDuration(0.5초) 동안 그 자리(공중이면 공중)에 멈춘 채 플레이어 쪽을 본다. 그림은 직전 그림(점프·발사의 마지막 그림)을 유지한다.
//    점프 꼭대기에서 멈춰 기다리는 시간도 이 준비다. 플레이어가 보고 피할 시간을 준다. 준비 중에는 몸에 닿아도 피해가 없다.
//  - 조준: 준비의 절반(JumpAttackAimTime)이 지났을 때의 플레이어 위치를 기억한다. 그 뒤에 플레이어가 움직여도 돌진 방향은 바뀌지 않는다.
//  - 돌진: 발사와 달리 그 위치에서 멈추지 않고, 그 방향으로 벽이나 땅에 닿을 때까지 계속 날아간다. 돌진하는 동안 중력을 끈다.
//    맵에 천장이 없으므로, 위로 향해 날아갈 때는 화면 위쪽 끝에 닿아도 멈춘다. (Mushroom.GetDistanceToScreenTop)
//    어디에도 닿지 못하는 예외 상황에는 JumpAttackMaxDuration이 지나면 끝난다.
//  - 돌진하는 동안 플레이어가 몸에 닿으면 피해를 입는다. 한 번의 점프공격에서 한 번만 맞는다. (플레이어와는 서로 관통한다)
// 벽이나 땅에 닿으면 그 자리에 멈춰 마무리 그림을 재생하고 카메라를 흔든다. (Mushroom.PlayImpactFeedbacks, 화면 위쪽 끝이나 시간 초과로 멈출 때는 흔들지 않는다)
//  부딪힌 자리에는 충격파(pixelFX heavy-landing, 벽이면 옆으로 돌림)가 터지고, 고리가 퍼지는 동안 같은 범위에 피해 판정이 나간다. (Mushroom.StartImpactWave)
//  충격파는 몸통과 판정 기록을 함께 써서, 한 번의 점프공격에서 둘 중 하나에만 맞는다.
// 벽에 닿아 공중에서 멈췄다면 중력으로 떨어진다. 마무리에는 피해가 없다.
// 마무리 그림이 끝나면 Idle로 쉰다. (공중이면 떨어져서 착지한 뒤, Mushroom.ChangeToRest)
// 애니메이션: JumpAttack. 돌진하는 동안 앞부분(Mushroom-Attack_5 ~ 7)을 재생하고 7에서 멈춰 두며, 닿으면 뒷부분(8 ~ 9)을 재생한다.
//  돌진할 때 돌진하는 쪽을 보며, 그러면 갓(머리)이 앞을 향한다. 끝난 뒤에도 그쪽을 본 채로 두며, 다음 패턴이 시작될 때 다시 플레이어 쪽을 본다.
//  날아가는 그림은 칸의 앞쪽에 치우쳐 그려져 있어, 그동안 그림을 뒤로 옮겨 몸 판정과 맞춘다. (JumpAttackSpriteOffsetX)
// 연출: 돌진하는 동안 몸 뒤로 속도선이 지나가고(돌진 방향으로 기울임), 벽·땅에 닿으면 끈다. (Mushroom.PlaySpeedLines / StopSpeedLines)
public class MushroomJumpAttackState : MushroomState
{
    // 조준점과의 거리가 이 값 이하면 방향을 정할 수 없는 것으로 보고 플레이어가 있는 쪽 가로로 돌진한다. (단위: 월드 거리)
    private const float MinAimDistance = 0.1f;

    private float _enterTime;
    // 플레이어 위치를 기억했는지 여부와 그 위치(루트 위치 기준)
    private bool _hasAim;
    private Vector2 _aimPoint;
    // 준비가 끝나고 돌진 중인지 여부
    private bool _isDashing;
    private float _dashStartTime;
    private Vector2 _direction;
    // 돌진하는 가로 방향 (오른쪽 1, 왼쪽 -1). 보는 방향과 몸 판정 범위의 좌우를 정한다.
    private float _facing;
    // 이번 물리 스텝으로 벽·땅(또는 화면 위쪽 끝)에 닿는 자리까지 옮겼는지. 다음 스텝에 닿은 것으로 처리한다.
    private bool _isArriving;
    // 다음 스텝에 닿는 것이 벽·땅인지(화면 위쪽 끝이 아니라). 닿을 때 카메라를 흔들지 정한다.
    private bool _isHittingTerrain;
    // 다음 스텝에 닿는 벽·땅 면의 법선(면에서 바깥쪽). 충격파의 방향을 정한다.
    private Vector2 _hitNormal;
    // 벽·땅에 닿아 멈춘 뒤인지 여부
    private bool _hasImpacted;

    public MushroomJumpAttackState(Mushroom owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        _hasAim = false;
        _isDashing = false;
        _isArriving = false;
        _isHittingTerrain = false;
        _hitNormal = Vector2.up;
        _hasImpacted = false;

        // 준비 동안 그 자리(공중이면 공중)에 멈춰 있도록 중력을 끈다. 그림은 직전 그림을 그대로 둔다.
        mushroom.Movement.SetGravityEnabled(false);
        mushroom.Movement.SetVelocity(Vector2.zero);

        if (!mushroom.IsTargetDead)
        {
            mushroom.FaceTarget();
        }
    }

    // 점프공격이 중간에 끊기는 경우(플레이어 사망, Mushroom 사망 등)에도 그 자리에 멈추고 중력과 그림 위치를 되돌린다.
    public override void Exit()
    {
        mushroom.StopSpeedLines();
        mushroom.Animation.SetSpriteOffsetX(0f);
        mushroom.Movement.SetGravityEnabled(true);

        if (_hasImpacted)
        {
            mushroom.Movement.SetVelocityX(0f);
        }
        else
        {
            mushroom.Movement.SetVelocity(Vector2.zero);
        }
    }

    public override void LogicUpdate()
    {
        if (!_isDashing)
        {
            return;
        }

        if (!_hasImpacted)
        {
            // 닿기 전에 마무리 그림(Attack_8 ~ 9)으로 넘어가지 않게 한다.
            mushroom.Animation.HoldBefore(MushroomAnimation.JumpAttack, MushroomAnimation.JumpAttackImpactProgress);
            return;
        }

        if (mushroom.Animation.IsFinished(MushroomAnimation.JumpAttack))
        {
            mushroom.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        if (!_isDashing)
        {
            float elapsedTime = Time.time - _enterTime;

            if (!_hasAim && elapsedTime >= mushroom.JumpAttackAimTime)
            {
                CaptureAim();
            }

            if (elapsedTime < mushroom.JumpAttackReadyDuration)
            {
                mushroom.Movement.SetVelocity(Vector2.zero);
                return;
            }

            StartDash();
        }

        if (_hasImpacted)
        {
            mushroom.Movement.SetVelocityX(0f);
            return;
        }

        // 지금 자리의 몸 판정을 먼저 낸다. 벽·땅에 닿은 자리에서도 마지막으로 판정한다.
        if (mushroom.HitChecker.Hit(mushroom.BodyHitbox, mushroom.JumpAttackDamage, _facing) > 0)
        {
            mushroom.AddHitGauge();
        }

        if (!IsCurrentState)
        {
            return;
        }

        if (_isArriving || Time.time - _dashStartTime >= mushroom.JumpAttackMaxDuration)
        {
            Impact(_isArriving && _isHittingTerrain);
            return;
        }

        // 이번 스텝에 벽·땅이나 화면 위쪽 끝에 닿으면 그 자리까지만 옮기고, 다음 스텝에 닿은 것으로 처리한다.
        float stepDistance = mushroom.JumpAttackSpeed * Time.fixedDeltaTime;
        float screenTopDistance = mushroom.GetDistanceToScreenTop(_direction);
        bool isBlocked = screenTopDistance <= stepDistance;
        stepDistance = Mathf.Min(stepDistance, screenTopDistance);

        _isHittingTerrain = mushroom.CastBody(_direction, stepDistance, out float freeDistance, out Vector2 hitNormal);

        if (_isHittingTerrain)
        {
            _hitNormal = hitNormal;
            isBlocked = true;
        }

        mushroom.Movement.SetVelocity(_direction * (freeDistance / Time.fixedDeltaTime));
        _isArriving = isBlocked;
    }

    // 지금 플레이어의 위치를 기억한다. 보는 방향은 바꾸지 않는다. (준비를 시작할 때 플레이어 쪽을 봤고, 돌진할 때 돌진하는 쪽을 본다)
    private void CaptureAim()
    {
        _hasAim = true;
        _aimPoint = mushroom.GetAimPoint();
    }

    // 준비를 마치고 기억해 둔 플레이어 쪽으로 돌진을 시작한다.
    private void StartDash()
    {
        if (!_hasAim)
        {
            CaptureAim();
        }

        Vector2 toAim = _aimPoint - mushroom.Movement.Position;
        float aimDistance = toAim.magnitude;
        _facing = mushroom.GetFacingToward(toAim.x);
        _direction = aimDistance > MinAimDistance ? toAim / aimDistance : new Vector2(_facing, 0f);

        // 땅에 서 있으면 아래로는 갈 수 없으므로 바닥을 따라 가로로만 돌진한다. (발 높이의 미세한 차이로 바닥에 바로 막히지 않게 한다)
        if (mushroom.Movement.IsGrounded && _direction.y < 0f)
        {
            _direction = new Vector2(_facing, 0f);
        }

        _isDashing = true;
        _dashStartTime = Time.time;

        mushroom.HitChecker.ResetHitTargets();
        // 돌진하는 쪽을 보면 날아가는 그림(Attack_5 ~ 7)의 갓(머리)이 앞을 향한다. 마무리 그림(8 ~ 9)도 이어지도록 닿은 뒤에도 그대로 둔다.
        mushroom.Animation.UpdateFacing(_facing);
        mushroom.Animation.Play(MushroomAnimation.JumpAttack);
        // 날아가는 그림은 칸의 앞쪽에 치우쳐 그려져 있어 뒤로 옮겨 몸 판정과 맞춘다.
        mushroom.Animation.SetSpriteOffsetX(-mushroom.JumpAttackSpriteOffsetX);
        mushroom.PlaySpeedLines(_direction);
    }

    // 벽·땅(또는 화면 위쪽 끝)에 닿았을 때: 그 자리에 멈추고 중력을 되돌린 뒤 마무리 그림을 재생한다.
    // hasHitTerrain: 벽이나 땅에 부딪혔는지. 그렇다면 카메라를 흔든다.
    private void Impact(bool hasHitTerrain)
    {
        _hasImpacted = true;
        mushroom.StopSpeedLines();

        if (hasHitTerrain)
        {
            mushroom.PlayImpactFeedbacks();
            mushroom.StartImpactWave(_hitNormal);
        }

        mushroom.Movement.SetGravityEnabled(true);
        mushroom.Movement.SetVelocity(Vector2.zero);
        mushroom.Animation.SetSpriteOffsetX(0f);
        mushroom.Animation.Play(MushroomAnimation.JumpAttack, MushroomAnimation.JumpAttackImpactProgress);
    }
}

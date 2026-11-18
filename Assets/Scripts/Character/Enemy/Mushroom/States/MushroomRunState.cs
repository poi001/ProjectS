using UnityEngine;

// 런(몸통박치기 달리기). 제자리에서 뛰는 준비 동작(RunReadyDuration, 0.5초) 뒤, 기억해 둔 플레이어의 가로 위치까지 바닥을 따라 아주 빠르게 달린다.
//  - 조준: 준비 동작의 절반(RunAimTime)이 지났을 때의 플레이어 가로 위치를 기억한다. 플레이어가 공중에 있어도 높이는 보지 않고 바닥에서 가로로만 움직인다.
//  - 달리기: 일정한 속도(RunSpeed)로 그 자리까지 달리고 도착하면 멈춘다. 가는 길에 벽에 막히면 그 자리에서 끝난다.
//    막히지도 도착하지도 못하는 예외 상황에는 RunMaxDuration이 지나면 끝난다.
//  - 방향: 준비를 시작할 때 플레이어 쪽으로 돌아서고, 준비 중에는 돌아서지 않는다. 달리기 시작할 때 달리는 방향으로 돌아선다.
//  - 달리는 동안 플레이어가 몸에 닿으면 피해를 입는다. 한 번의 런에서 한 번만 맞는다. 준비 동작에는 피해가 없다. (플레이어와는 서로 관통한다)
// 달리는 동안 중력을 끄고 가로로만 움직인다. 중력이 켜져 있으면 바닥 마찰 때문에 정한 속도보다 느려진다. (Knight 슬라이딩과 같다)
// 끝나면 Idle 또는 발사로 넘어간다. (Mushroom.ChangeAfterRun)
// 애니메이션: Run(Mushroom-Run_0 ~ 7 반복, 0.25초에 한 바퀴). 준비 동작과 달리는 동안 함께 쓴다.
// 연출: 준비 동작부터 끝날 때까지 발밑 먼지(pixelFX impacts/run-dust)가 뒤로 흩날린다. (Mushroom.PlayRunDust / StopRunDust)
//  달리는 동안(준비 동작 제외)에는 몸 뒤로 속도선이 지나간다. (Mushroom.PlaySpeedLines / StopSpeedLines)
public class MushroomRunState : MushroomState
{
    // 남은 거리가 이 값 이하면 도착한 것으로 본다. (단위: 월드 거리)
    private const float ArriveDistance = 0.001f;

    private float _enterTime;
    // 플레이어의 가로 위치를 기억했는지 여부와 그 위치(루트 위치 기준)
    private bool _hasAim;
    private float _aimX;
    // 준비 동작이 끝나고 달리는 중인지 여부
    private bool _isRunning;
    private float _runStartTime;
    // 달리는 방향 (오른쪽 1, 왼쪽 -1)
    private float _direction;
    private float _remainingDistance;
    // 이번 물리 스텝으로 목표 지점이나 벽에 닿는 자리까지 옮겼는지. 다음 스텝에 끝난 것으로 처리한다.
    private bool _isArriving;

    public MushroomRunState(Mushroom owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        _hasAim = false;
        _isRunning = false;
        _isArriving = false;

        mushroom.Movement.SetVelocityX(0f);

        if (!mushroom.IsTargetDead)
        {
            mushroom.FaceTarget();
        }

        mushroom.Animation.Play(MushroomAnimation.Run);
        mushroom.PlayRunDust();
    }

    // 런이 중간에 끊기는 경우(플레이어 사망, Mushroom 사망 등)에도 그 자리에 멈추고 중력을 되돌리며 발밑 먼지를 끈다.
    public override void Exit()
    {
        mushroom.StopRunDust();
        mushroom.StopSpeedLines();

        if (_isRunning)
        {
            mushroom.Movement.SetGravityEnabled(true);
            mushroom.Movement.SetVelocity(Vector2.zero);
        }
        else
        {
            mushroom.Movement.SetVelocityX(0f);
        }
    }

    public override void LogicUpdate()
    {
        if (_isRunning)
        {
            return;
        }

        float elapsedTime = Time.time - _enterTime;

        if (!_hasAim && elapsedTime >= mushroom.RunAimTime)
        {
            CaptureAim();
        }

        // 바닥을 따라 달리므로 준비가 끝나도 땅에 닿아 있을 때 출발한다.
        if (elapsedTime >= mushroom.RunReadyDuration && mushroom.Movement.IsGrounded)
        {
            StartRun();
        }
    }

    public override void PhysicsUpdate()
    {
        if (!_isRunning)
        {
            mushroom.Movement.SetVelocityX(0f);
            return;
        }

        // 지금 자리의 몸 판정을 먼저 낸다. 도착한 자리에서도 마지막으로 판정한다.
        if (mushroom.HitChecker.Hit(mushroom.BodyHitbox, mushroom.RunDamage, _direction) > 0)
        {
            mushroom.MarkPlayerHit();
            mushroom.AddHitGauge();
        }

        if (!IsCurrentState)
        {
            return;
        }

        if (_isArriving || Time.time - _runStartTime >= mushroom.RunMaxDuration)
        {
            mushroom.ChangeAfterRun();
            return;
        }

        // 이번 스텝에 목표 지점이나 벽에 닿으면 그 자리까지만 옮기고, 다음 스텝에 끝난 것으로 처리한다.
        float stepDistance = Mathf.Min(mushroom.RunSpeed * Time.fixedDeltaTime, _remainingDistance);
        bool isBlocked = mushroom.CastBody(new Vector2(_direction, 0f), stepDistance, out float freeDistance);

        mushroom.Movement.SetVelocity(new Vector2(_direction * (freeDistance / Time.fixedDeltaTime), 0f));
        _remainingDistance -= freeDistance;
        _isArriving = isBlocked || _remainingDistance <= ArriveDistance;
    }

    // 지금 플레이어의 가로 위치를 기억한다. 보는 방향은 바꾸지 않는다. (준비를 시작할 때 플레이어 쪽을 봤고, 달리기 시작할 때 달리는 방향을 본다)
    private void CaptureAim()
    {
        _hasAim = true;
        _aimX = mushroom.GetAimPoint().x;
    }

    private void StartRun()
    {
        if (!_hasAim)
        {
            CaptureAim();
        }

        float offsetX = _aimX - mushroom.Movement.Position.x;
        _remainingDistance = Mathf.Abs(offsetX);
        _direction = _remainingDistance > ArriveDistance ? Mathf.Sign(offsetX) : mushroom.Animation.FacingDirection;
        _isArriving = _remainingDistance <= ArriveDistance;
        _isRunning = true;
        _runStartTime = Time.time;

        mushroom.HitChecker.ResetHitTargets();
        mushroom.ResetPlayerHit();
        mushroom.Movement.SetGravityEnabled(false);
        mushroom.Movement.SetVelocity(Vector2.zero);
        // 달리는 방향으로 돌아선다. (제자리에 도착해 있으면 _direction이 지금 보는 방향이라 그대로다)
        mushroom.Animation.UpdateFacing(_direction);
        mushroom.PlaySpeedLines(new Vector2(_direction, 0f));
    }
}

using UnityEngine;

// 반응속도 기믹. 패턴 게이지가 꽉 찬 채로 Idle에 들어가면 시작한다. (Mushroom.TryInterruptIdle) 시작할 때 게이지를 비운다.
// 플레이어의 반응속도를 보는 기믹이다. 기믹 내내 시네마틱(위아래 검은 띠, 줌인, 전투 UI 숨김)이 켜져 있다.
//  1) 자리 배치(Setup): 카메라가 줌인하는 동안(ReactionSetupDuration) Mushroom과 플레이어를 줌인한 화면의 양쪽 자리로 미끄러지듯 옮긴다.
//     서로 마주 보게 하고, 둘 다 땅에 닿아 있어야 다음으로 넘어간다. (플레이어가 공중이면 착지할 때까지, 길어도 LandingWaitTimeout까지 기다린다) 조작은 모두 잠겨 있다.
//  2) 대기(Wait): 발사 준비자세(Launch의 앞부분, Attack_3에서 멈춤)로 ReactionMinWait ~ ReactionMaxWait(2~10초) 중 무작위 시간을 기다린다.
//     이때부터 플레이어는 공격만 할 수 있다. (이동·점프·대시는 잠김) 콤보는 막아 항상 1타만 나가고, 한 번 공격하면 1초(PlayerAttackInterval) 동안 다시 공격할 수 없다. 미리 공격해도 벌칙은 없지만 멀어서 닿지 않는다.
//  3) 발사(Launch): 플레이어가 서 있는 자리까지 바닥을 따라 ReactionLaunchSpeed로 날아간다.
//     - 몸이 플레이어에게 닿기 전에 플레이어가 먼저 피해를 주면 성공: 그 자리에서 스턴에 걸린다. (Mushroom.ChangeToStun)
//     - 먼저 닿으면 실패: 공격력의 3배 피해(ReactionDamage)를 입히고, 플레이어가 서 있던 자리까지 가서 멈춘 뒤 Idle로 쉰다.
//     - 막히지도 도착하지도 못하는 예외 상황에는 도착에 걸릴 시간(거리 / 속도)보다 LaunchTimeoutMargin만큼 더 지나면 끝난다.
// 결과가 난 순간(성공·실패)에는 결과 연출이 나온다: 맞은 지점의 임팩트 프레임, 맞은 쪽의 기믹 전용 효과음(KO 소리를 변형한 것), 슬로우 모션. (Mushroom.PlayReactionResult)
//  그동안(실제 0.6초) 시네마틱을 유지했다가 닫는다. 그 결과로 누군가 죽으면 임팩트 프레임만 띄우고 바로 KO 연출로 넘어간다.
// 결과 없이 끊기면(전투 종료 등) 나갈 때 바로 시네마틱을 닫는다. 플레이어의 공격 허용도 시네마틱이 끝나며 함께 꺼진다.
// 애니메이션: 자리 배치는 Idle, 대기는 Launch 앞부분(Attack_0 ~ 3), 발사는 Launch 뒷부분(Attack_4 한 장 유지)
// 연출: 준비자세로 기다리는 동안 몸 한가운데로 노란 빛 고리가 모여든다. (pixelFX arcane/energy-gather 고리 방식, Mushroom.PlayReactionCharge / StopReactionCharge)
//  발사하는 동안에는 몸 뒤로 속도선이 지나간다. (Mushroom.PlaySpeedLines / StopSpeedLines)
public class MushroomReactionState : MushroomState
{
    private enum Phase
    {
        Setup,
        Wait,
        Launch,
    }

    // 남은 거리가 이 값 이하면 도착한 것으로 본다. (단위: 월드 거리)
    private const float ArriveDistance = 0.001f;
    // 자리 배치가 끝난 뒤 둘 다 땅에 닿기를 기다리는 최대 시간. 넘으면 닿지 않았어도 대기로 넘어간다. (단위: 초)
    // 착지 판정이 서지 않는 예외 상황에서 시네마틱이 열린 채 조작이 계속 잠겨 있지 않게 한다.
    private const float LandingWaitTimeout = 3f;
    // 발사가 도착에 걸릴 시간(거리 / 속도)보다 이만큼 더 지나도 끝나지 않으면 끝낸다. 발사 속도를 낮춰도 도착하기 전에 끊기지 않게 거리로 계산한다. (단위: 초)
    private const float LaunchTimeoutMargin = 0.5f;
    // 대기 동안 플레이어가 공격을 시작한 뒤 다음 공격을 시작할 수 있기까지의 간격. 공격 동작 길이와 상관없이 항상 이 시간이다.
    // 이 동안 누른 공격 입력은 버려진다. 연타로 공격을 빠르게 반복해 성공 타이밍을 우연히 맞히지 못하게 한다. (단위: 초)
    private const float PlayerAttackInterval = 1f;

    private Phase _phase;
    // 자리 배치: 미끄러지기 시작한 가로 위치와 목표 위치(루트 위치 기준), 지난 시간(물리 스텝마다 더한다)
    private float _setupStartX;
    private float _setupTargetX;
    private float _setupElapsedTime;
    // 대기: 준비자세를 잡은 시각과 이번 기믹에서 기다릴 시간
    private float _waitStartTime;
    private float _waitDuration;
    // 발사: 날아가는 가로 방향(오른쪽 1, 왼쪽 -1), 남은 거리, 시작 시각
    private float _direction;
    private float _remainingDistance;
    private float _launchStartTime;
    // 이 시간이 지나도 도착하지 못하면 발사를 끝낸다. (StartLaunch에서 거리와 속도로 정한다)
    private float _launchTimeout;
    // 이번 물리 스텝으로 목표 지점이나 벽에 닿는 자리까지 옮겼는지. 다음 스텝에 끝난 것으로 처리한다.
    private bool _isArriving;
    // 몸이 플레이어에게 닿아 피해를 줬는지(실패 확정). 그 뒤에 플레이어가 때려도 스턴에 걸리지 않는다.
    private bool _hasHitTarget;

    public MushroomReactionState(Mushroom owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _phase = Phase.Setup;
        _setupElapsedTime = 0f;
        _isArriving = false;
        _hasHitTarget = false;

        // 기믹이 시작되면 게이지를 비운다. 기믹과 스턴이 끝난 뒤부터 다시 찬다.
        mushroom.PatternGauge.SetValue(0f);

        mushroom.GetReactionLayout(out _setupTargetX, out float targetX, out Vector2 cameraCenter);
        _setupStartX = mushroom.Movement.Position.x;

        // 자리에 선 뒤 서로 마주 보는 방향
        float facing = targetX >= _setupTargetX ? 1f : -1f;

        mushroom.BeginReactionCinematic(cameraCenter);
        mushroom.Movement.SetVelocityX(0f);
        mushroom.Animation.UpdateFacing(facing);
        mushroom.Animation.Play(EnemyAnimation.Idle);
        mushroom.Target.StartCinematicSlide(targetX, mushroom.ReactionSetupDuration, -facing);
    }

    // 기믹이 중간에 끊기는 경우(플레이어 사망, Mushroom 사망 등)에도 그 자리에 멈추고 중력·속도선·시네마틱을 되돌린다.
    public override void Exit()
    {
        mushroom.StopSpeedLines();
        mushroom.StopReactionCharge();

        if (_phase == Phase.Launch)
        {
            mushroom.Movement.SetGravityEnabled(true);
            mushroom.Movement.SetVelocity(Vector2.zero);
        }
        else
        {
            mushroom.Movement.SetVelocityX(0f);
        }

        // 결과 연출(임팩트 프레임·슬로우 모션)이 진행 중이면 시네마틱은 그 연출이 끝날 때 닫힌다.
        mushroom.EndReactionCinematicAfterResult();
    }

    public override void LogicUpdate()
    {
        if (_phase != Phase.Wait)
        {
            return;
        }

        if (Time.time - _waitStartTime < _waitDuration)
        {
            // 발사하기 전에 날아가는 그림(Attack_4)으로 넘어가지 않게 한다.
            mushroom.Animation.HoldBefore(MushroomAnimation.Launch, MushroomAnimation.LaunchFlyProgress);
            return;
        }

        StartLaunch();
    }

    public override void PhysicsUpdate()
    {
        switch (_phase)
        {
            case Phase.Setup:
                UpdateSetup();
                break;
            case Phase.Wait:
                mushroom.Movement.SetVelocityX(0f);
                break;
            case Phase.Launch:
                UpdateLaunch();
                break;
        }
    }

    // 기믹 중에 플레이어에게 피해를 입었을 때 Mushroom이 호출한다.
    // 발사 도중이고 아직 플레이어에게 닿지 않았다면 성공으로 보고 스턴에 건다. 그 밖의 때(자리 배치·대기 중, 이미 닿은 뒤)에는 아무 일도 없다.
    // hitPoint: 맞은 지점(월드 좌표). 결과 연출의 임팩트 프레임이 이 지점을 중심으로 뜬다.
    // 이 피해로 Mushroom이 죽는 경우에도 결과 연출은 시작하며, 스턴 대신 사망 상태로 넘어간다. (ChangeToStun은 죽었으면 아무것도 하지 않는다)
    public void OnDamagedByTarget(Vector2 hitPoint)
    {
        if (_phase != Phase.Launch || _hasHitTarget)
        {
            return;
        }

        mushroom.PlayReactionResult(hitPoint, false);
        mushroom.ChangeToStun();
    }

    // 처음과 끝이 느린 곡선(SmoothStep)으로 목표 자리까지 미끄러진다. 다음 스텝의 목표 자리까지 가는 속도를 매번 다시 계산해 오차가 쌓이지 않게 한다.
    // 시간이 다 되면 멈추고, Mushroom과 플레이어가 모두 땅에 닿아 있을 때 대기로 넘어간다. (LandingWaitTimeout이 지나면 닿지 않았어도 넘어간다)
    private void UpdateSetup()
    {
        if (_setupElapsedTime < mushroom.ReactionSetupDuration)
        {
            _setupElapsedTime += Time.fixedDeltaTime;
            float progress = Mathf.Clamp01(_setupElapsedTime / mushroom.ReactionSetupDuration);
            float nextX = Mathf.Lerp(_setupStartX, _setupTargetX, Mathf.SmoothStep(0f, 1f, progress));
            mushroom.Movement.SetVelocityX((nextX - mushroom.Movement.Position.x) / Time.fixedDeltaTime);
            return;
        }

        mushroom.Movement.SetVelocityX(0f);
        _setupElapsedTime += Time.fixedDeltaTime;

        bool isLanded = mushroom.Movement.IsGrounded && mushroom.Target.Movement.IsGrounded;
        bool isTimedOut = _setupElapsedTime >= mushroom.ReactionSetupDuration + LandingWaitTimeout;

        if (isLanded || isTimedOut)
        {
            StartWait();
        }
    }

    // 발사 준비자세를 잡고, 이번에 기다릴 시간을 무작위로 정한다. 이때부터 플레이어는 공격만 할 수 있다.
    private void StartWait()
    {
        _phase = Phase.Wait;
        _waitStartTime = Time.time;
        _waitDuration = Random.Range(mushroom.ReactionMinWait, mushroom.ReactionMaxWait);

        mushroom.FaceTarget();
        mushroom.Animation.Play(MushroomAnimation.Launch);
        // 공격은 허용하되 콤보는 막아 1타만 나가게 하고, 한 번 공격하면 PlayerAttackInterval 동안 다시 공격하지 못하게 한다.
        mushroom.Target.SetCinematicAttackAllowed(true, false, PlayerAttackInterval);
        mushroom.PlayReactionCharge();
    }

    // 지금 플레이어가 서 있는 자리까지 바닥을 따라 날아가기 시작한다. 날아가는 동안 중력을 끄고 가로로만 움직인다.
    private void StartLaunch()
    {
        float offsetX = mushroom.GetAimPoint().x - mushroom.Movement.Position.x;
        _remainingDistance = Mathf.Abs(offsetX);
        _direction = _remainingDistance > ArriveDistance ? Mathf.Sign(offsetX) : mushroom.Animation.FacingDirection;
        _isArriving = _remainingDistance <= ArriveDistance;
        _phase = Phase.Launch;
        _launchStartTime = Time.time;
        _launchTimeout = _remainingDistance / mushroom.ReactionLaunchSpeed + LaunchTimeoutMargin;

        mushroom.HitChecker.ResetHitTargets();
        mushroom.Movement.SetGravityEnabled(false);
        mushroom.Movement.SetVelocity(Vector2.zero);
        // 날아가는 쪽을 보면 그림(Attack_4)의 갓(머리)이 앞을 향한다.
        mushroom.Animation.UpdateFacing(_direction);
        mushroom.Animation.Play(MushroomAnimation.Launch, MushroomAnimation.LaunchFlyProgress);
        mushroom.StopReactionCharge();
        mushroom.PlaySpeedLines(new Vector2(_direction, 0f));
    }

    private void UpdateLaunch()
    {
        // 지금 자리의 몸 판정을 먼저 낸다. 닿으면 실패가 확정된다. (한 번의 발사에서 한 번만 맞는다)
        if (!_hasHitTarget && mushroom.HitChecker.Hit(mushroom.BodyHitbox, mushroom.ReactionDamage, _direction) > 0)
        {
            _hasHitTarget = true;
            // 맞은 지점은 플레이어 몸에서 Mushroom 몸 가운데에 가장 가까운 점으로 본다.
            Vector2 hitPoint = mushroom.Target.BodyCollider.ClosestPoint(mushroom.BodyCollider.bounds.center);
            mushroom.PlayReactionResult(hitPoint, true);
        }

        // 이 피해로 플레이어가 죽으면 그 자리에서 Idle로 바뀌므로, 남은 처리를 하지 않는다.
        if (!IsCurrentState)
        {
            return;
        }

        if (_isArriving || Time.time - _launchStartTime >= _launchTimeout)
        {
            mushroom.ChangeToRest();
            return;
        }

        // 이번 스텝에 목표 지점이나 벽에 닿으면 그 자리까지만 옮기고, 다음 스텝에 끝난 것으로 처리한다.
        float stepDistance = Mathf.Min(mushroom.ReactionLaunchSpeed * Time.fixedDeltaTime, _remainingDistance);
        bool isBlocked = mushroom.CastBody(new Vector2(_direction, 0f), stepDistance, out float freeDistance);

        mushroom.Movement.SetVelocity(new Vector2(_direction * (freeDistance / Time.fixedDeltaTime), 0f));
        _remainingDistance -= freeDistance;
        _isArriving = isBlocked || _remainingDistance <= ArriveDistance;
    }
}

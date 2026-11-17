using MoreMountains.Feedbacks;
using UnityEngine;

// SkullDog(스컬독) 보스. 1페이즈만 있는 지상형 보스다.
// 흐름: Idle → (무작위) 대시 준비(DashReady) → 대시(Dash) → 대시 마무리(DashEnd) → Idle,
//       또는 런(Run) → 벽 충돌 → 스턴(Stun) → Idle
//  - 대시: 준비를 시작하는 순간의 플레이어 위치를 기억해 두고, 준비가 끝나면 그 자리로 빠르게 일직선 돌진한다.
//    플레이어가 공중에 있었다면 공중으로 돌진하고, 도착하면 그 자리에서 중력으로 떨어진다. 돌진하는 동안 몸에 닿은 플레이어가 피해를 입는다. (한 번의 대시에서 한 번만)
//  - 런: 플레이어가 있는 쪽으로 벽에 부딪힐 때까지 계속 달린다. 처음에는 느리고 시간이 지날수록 빨라진다.
//    시작하고 Run Grace Duration(0.5초) 동안은 벽에 닿아도 스턴에 걸리지 않고 플레이어도 피해를 입지 않는다.
//    플레이어가 반대쪽으로 넘어가면 바로 돌아서지 못하고 미끄러지며 속도를 줄인 뒤 돌아선다. (관성)
//    달리는 동안 몸에 닿은 플레이어가 피해를 입는다. 플레이어의 피격 무적 시간이 끝난 뒤에도 닿아 있으면 다시 맞는다.
//  - 스턴: 런으로 벽에 부딪히면 걸린다. 부딪힌 순간의 속도가 빠를수록 오래 기절한다.
// 패턴 게이지는 쓰지 않는다.
public class SkullDog : Enemy
{
    private const string GroundLayerName = "Ground";
    private const string WallLayerName = "Wall";
    // 돌진 중 지형을 찾을 때 한 번에 처리할 최대 결과 수
    private const int MaxCastCount = 4;
    // 돌진하다 지형에 닿을 때 콜라이더 사이에 남기는 간격 (단위: 월드 거리)
    private const float CastContactSkin = 0.02f;
    // 지형을 찾는 박스를 몸 콜라이더보다 사방으로 이만큼 줄인다. 발밑 바닥처럼 이미 맞닿아 있는 지형이 겹친 것으로 잡히지 않게 한다. (단위: 월드 거리)
    private const float CastShrink = 0.02f;
    // 닿은 면이 움직이는 방향을 이 값보다 더 마주 볼 때만 막힌 것으로 본다. (면의 법선과 이동 방향의 내적, 0이면 스치는 면)
    private const float BlockingNormalDot = -0.05f;

    [Header("패턴")]
    [Tooltip("패턴(대시, 런)이 끝난 뒤 다음 패턴을 시전하기 전까지 Idle로 쉬는 시간입니다. 전투 시작 때의 Idle 시간은 '시작' 항목의 Start Idle Duration에서 따로 정합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _patternInterval = 1f;
    [Tooltip("Idle이 끝났을 때 대시를 고를 확률입니다. 0이면 항상 런, 1이면 항상 대시이고, 나머지 경우는 런입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _dashChance = 0.5f;

    [Header("몸 충돌 (대시·런 공통)")]
    [Tooltip("대시로 돌진하거나 런으로 달리는 동안의 몸 충돌 판정 범위입니다. 이 범위에 닿은 플레이어가 피해를 입습니다. 스컬독 그림 크기에 맞춘 값입니다.")]
    [SerializeField] private HitboxShape _bodyHitbox = new HitboxShape(Vector2.zero, new Vector2(3.6f, 1.4f));

    [Header("대시")]
    [Tooltip("대시 준비 자세(전조) 시간입니다. 준비를 시작하는 순간의 플레이어 위치를 기억해 두고, 이 시간이 지나면 그 자리로 돌진합니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _dashReadyDuration = 0.5f;
    [Tooltip("기억해 둔 플레이어 위치로 일직선 돌진하는 속도입니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _dashSpeed = 30f;
    [Tooltip("돌진이 목표 지점에 닿지 못하는 예외 상황에서 돌진을 끝내는 최대 시간입니다. (단위: 초)")]
    [SerializeField, Min(0.1f)] private float _dashMaxDuration = 1f;
    [Tooltip("돌진 중 몸에 닿은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 한 번의 대시에서 한 번만 맞습니다. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _dashDamageMultiplier = 1f;
    [Tooltip("대시 속도선 효과의 회전 기준입니다. Visuals/MirroredVFX/Dash/DashLines를 연결합니다. 돌진하는 동안 돌진 방향으로 기울입니다. 켜고 끄는 것은 아래 두 피드백이 맡습니다.\n비워 두면 기울이지 않습니다.")]
    [SerializeField] private Transform _dashLines;
    [Tooltip("돌진을 시작할 때 속도선을 켜는 피드백입니다. Visuals/MirroredVFX/Dash/DashLines_Enter의 MMF_Player를 연결합니다.\n비워 두면 속도선이 나오지 않습니다.")]
    [SerializeField] private MMF_Player _dashLinesEnter;
    [Tooltip("돌진이 끝나거나 중간에 끊길 때 속도선을 끄는 피드백입니다. Visuals/MirroredVFX/Dash/DashLines_Exit의 MMF_Player를 연결합니다.")]
    [SerializeField] private MMF_Player _dashLinesExit;

    [Header("런")]
    [Tooltip("런을 시작할 때의 속도입니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _runStartSpeed = 4f;
    [Tooltip("런을 하는 동안 1초마다 빨라지는 속도입니다. 클수록 빨리 최고 속도에 닿습니다. 방향을 바꿔도 처음 속도로 돌아가지 않고 계속 빨라집니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초²)")]
    [SerializeField, Min(0f)] private float _runAcceleration = 3f;
    [Tooltip("런의 최고 속도입니다. 이 속도에 닿으면 더 빨라지지 않습니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _runMaxSpeed = 16f;
    [Tooltip("관성의 세기입니다. 플레이어가 반대쪽으로 넘어갔을 때 1초에 속도를 이만큼 바꿉니다. 작을수록 오래 미끄러진 뒤에야 돌아서고, 클수록 바로 돌아섭니다. Run Acceleration보다 작으면 그 값으로 적용됩니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초²)")]
    [SerializeField, Min(0.1f)] private float _runTurnAcceleration = 20f;
    [Tooltip("달리는 동안 몸에 닿은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 플레이어의 피격 무적 시간이 끝난 뒤에도 닿아 있으면 다시 맞습니다. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _runDamageMultiplier = 1f;
    [Tooltip("런을 시작한 직후의 유예 시간입니다. 이 시간 동안에는 벽에 닿아도 스턴에 걸리지 않고(벽 쪽으로는 가지 못한 채 속도가 쌓이지 않습니다) 몸에 닿은 플레이어도 피해를 입지 않습니다. 시간이 지나면 벽 충돌과 몸 충돌 판정이 정상으로 들어갑니다. 벽에 붙은 채 런이 나왔을 때 바로 스턴에 걸리는 것을 막습니다. 0이면 유예 없이 시작합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _runGraceDuration = 0.5f;

    [Header("스턴")]
    [Tooltip("런을 시작할 때의 속도(Run Start Speed) 이하로 벽에 부딪혔을 때의 스턴 시간입니다. 기획이 정해지기 전의 임시값입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _stunMinDuration = 1f;
    [Tooltip("런의 최고 속도(Run Max Speed)로 벽에 부딪혔을 때의 스턴 시간입니다. 그 사이의 속도는 속도에 비례해 Stun Min Duration과 이 값 사이로 정해집니다. 기획이 정해지기 전의 임시값입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _stunMaxDuration = 3f;
    [Tooltip("스턴하는 동안 머리 위(StatusVfxDisplay의 Anchor)에 띄울 VFX 프리팹입니다. 모든 보스가 함께 쓰는 Assets/Prefabs/FX/StatusVfx_Stun을 연결합니다. 반복 재생되는 Flipbook과 Manual 반환 PooledObject를 가진 프리팹을 씁니다.\n비워 두면 VFX 없이 스턴합니다.")]
    [SerializeField] private GameObject _stunVfxPrefab;
    [Tooltip("런으로 벽에 부딪혀 스턴에 걸릴 때 재생하는 피드백입니다. (카메라 흔들림, Knight 슬라이딩·BloodWing 대시 충돌과 같은 값) 루트 VFX/Impact/ImpactFeedbacks의 MMF_Player를 연결합니다.")]
    [SerializeField] private MMF_Player _impactFeedbacks;

    // 돌진을 멈추는 지형(바닥, 벽)을 찾는 조건
    private ContactFilter2D _obstacleFilter;
    private readonly RaycastHit2D[] _castResults = new RaycastHit2D[MaxCastCount];

    public SkullDogDashReadyState DashReadyState { get; private set; }
    public SkullDogDashState DashState { get; private set; }
    public SkullDogDashEndState DashEndState { get; private set; }
    public SkullDogRunState RunState { get; private set; }
    public SkullDogStunState StunState { get; private set; }

    public HitboxShape BodyHitbox => _bodyHitbox;
    public float DashReadyDuration => _dashReadyDuration;
    public float DashSpeed => _dashSpeed;
    public float DashMaxDuration => _dashMaxDuration;
    public float DashDamage => Stats.AttackPower * _dashDamageMultiplier;
    public float RunStartSpeed => _runStartSpeed;
    public float RunAcceleration => _runAcceleration;
    // 최고 속도를 시작 속도보다 작게 넣어도 시작 속도 밑으로 내려가지 않게 한다.
    public float RunMaxSpeed => Mathf.Max(_runStartSpeed, _runMaxSpeed);
    // 속도가 런의 가속을 따라가지 못하면 점점 빨라지지 않으므로 그보다 작아지지 않게 한다.
    public float RunTurnAcceleration => Mathf.Max(_runTurnAcceleration, _runAcceleration);
    public float RunDamage => Stats.AttackPower * _runDamageMultiplier;
    public float RunGraceDuration => _runGraceDuration;
    public GameObject StunVfxPrefab => _stunVfxPrefab;

    protected override bool OnInit(BattleContext context)
    {
        if (!base.OnInit(context))
        {
            return false;
        }

        _obstacleFilter = new ContactFilter2D();
        _obstacleFilter.SetLayerMask(LayerMask.GetMask(GroundLayerName, WallLayerName));
        _obstacleFilter.useTriggers = false;

        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.Initialization();
        }

        if (_stunVfxPrefab != null)
        {
            if (StatusVfx == null)
            {
                Debug.LogWarning("SkullDog 루트에 StatusVfxDisplay가 없어 스턴 VFX를 띄우지 않습니다.", this);
            }
            else if (PoolManager.HasInstance)
            {
                // 첫 스턴 때 생성 비용으로 끊기지 않도록 미리 하나 만들어 둔다.
                PoolManager.Instance.Prewarm(_stunVfxPrefab, 1);
            }
        }

        // 켜는 피드백은 초기화할 때 속도선을 꺼 둔다.
        if (_dashLinesEnter != null)
        {
            _dashLinesEnter.Initialization();
        }

        if (_dashLinesExit != null)
        {
            _dashLinesExit.Initialization();
        }

        return true;
    }

    // 충돌 피드백(카메라 흔들림) 도중에 소환 해제되어도 흔들림이 남지 않게 멈춘다.
    protected override void OnRelease()
    {
        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.StopFeedbacks();
        }

        base.OnRelease();
    }

    protected override void CreateStates()
    {
        base.CreateStates();
        DashReadyState = new SkullDogDashReadyState(this, stateMachine);
        DashState = new SkullDogDashState(this, stateMachine);
        DashEndState = new SkullDogDashEndState(this, stateMachine);
        RunState = new SkullDogRunState(this, stateMachine);
        StunState = new SkullDogStunState(this, stateMachine);
    }

    // Idle 시간이 끝났을 때 호출된다. (플레이어가 죽어 있는 동안에는 호출되지 않는다)
    // 대시와 런 중 하나를 Dash Chance 확률로 고른다.
    public override void ChangeToNextPattern()
    {
        if (Random.value < _dashChance)
        {
            stateMachine.ChangeState(DashReadyState);
        }
        else
        {
            stateMachine.ChangeState(RunState);
        }
    }

    // 패턴이 끝난 뒤 Idle로 쉬어간다. (Pattern Interval)
    public void ChangeToRest()
    {
        ChangeToIdle(_patternInterval);
    }

    // 런으로 벽에 부딪혔을 때 SkullDogRunState가 호출한다. 부딪힌 순간의 속도(impactSpeed)에 맞는 시간 동안 스턴에 걸린다.
    // 스턴 상태로 바꿨으면 true를 반환한다. 죽었거나 페이즈 전환 중이면 아무것도 하지 않는다.
    public bool ChangeToStun(float impactSpeed)
    {
        if (!IsInitialized || Vital.IsDead || IsPhaseTransitioning)
        {
            return false;
        }

        StunState.SetDuration(GetStunDuration(impactSpeed));
        stateMachine.ChangeState(StunState);
        return true;
    }

    // 벽에 부딪힌 속도에 맞는 스턴 시간. 런 시작 속도 이하면 최소, 최고 속도면 최대이고 그 사이는 속도에 비례한다.
    public float GetStunDuration(float impactSpeed)
    {
        float speedRatio = Mathf.InverseLerp(_runStartSpeed, RunMaxSpeed, impactSpeed);
        return Mathf.Lerp(_stunMinDuration, _stunMaxDuration, speedRatio);
    }

    // 대시가 향할 지점(루트 위치 기준). 지금 플레이어의 가로 위치이고, 높이는 스컬독의 발이 플레이어의 발 높이에 오는 자리다.
    // 플레이어가 바닥에 서 있으면 바닥을 따라 가로로 돌진하고, 공중에 있으면 그 높이까지 날아오른다.
    // 플레이어가 없으면(죽음) 지금 자리다.
    public Vector2 GetDashAimPoint()
    {
        Vector2 position = Movement.Position;

        if (IsTargetDead)
        {
            return position;
        }

        Bounds bodyBounds = BodyCollider.bounds;
        Bounds targetBounds = Target.BodyCollider.bounds;
        float aimX = targetBounds.center.x + (position.x - bodyBounds.center.x);
        float aimY = targetBounds.min.y + (position.y - bodyBounds.min.y);
        return new Vector2(aimX, aimY);
    }

    // 몸 콜라이더를 direction(단위 벡터)으로 distance만큼 옮길 때 벽·바닥에 막히는지 확인한다.
    // 막히면 true와 함께, 닿기 직전까지 옮길 수 있는 거리를 freeDistance로 준다. 안 막히면 freeDistance는 distance다.
    // 움직이는 방향을 마주 보는 면만 막힌 것으로 본다. (바닥을 따라 가로로 돌진할 때 발밑 바닥은 막는 것이 아니다)
    public bool CastBody(Vector2 direction, float distance, out float freeDistance)
    {
        Bounds bounds = BodyCollider.bounds;
        Vector2 castSize = new Vector2(Mathf.Max(0.01f, bounds.size.x - CastShrink * 2f), Mathf.Max(0.01f, bounds.size.y - CastShrink * 2f));
        int hitCount = Physics2D.BoxCast(bounds.center, castSize, 0f, direction, _obstacleFilter, _castResults, distance + CastShrink + CastContactSkin);
        float nearest = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            if (Vector2.Dot(_castResults[i].normal, direction) > BlockingNormalDot)
            {
                continue;
            }

            nearest = Mathf.Min(nearest, _castResults[i].distance);
        }

        if (nearest == float.MaxValue)
        {
            freeDistance = distance;
            return false;
        }

        freeDistance = Mathf.Clamp(nearest - CastShrink - CastContactSkin, 0f, distance);
        return true;
    }

    // 돌진을 시작할 때 SkullDogDashState가 호출한다. 속도선을 돌진 방향(direction, 단위 벡터)으로 기울이고 켠다.
    // 속도선은 좌우 반전을 맡는 Visuals 아래에 있으므로 보는 쪽(앞)을 +x로 둔 각도로 기울이면, 왼쪽을 볼 때는 부모의 반전이 맞춰 준다.
    // 돌진 중에는 보는 방향이 돌진 방향의 가로 부호와 같다. (수직 돌진은 어느 쪽을 보든 위·아래로 90도다)
    public void PlayDashLines(Vector2 direction)
    {
        if (_dashLines != null)
        {
            float angle = Mathf.Atan2(direction.y, Mathf.Abs(direction.x)) * Mathf.Rad2Deg;
            _dashLines.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (_dashLinesEnter != null)
        {
            _dashLinesEnter.PlayFeedbacks();
        }
    }

    // 돌진이 끝나거나 끊길 때 SkullDogDashState가 호출한다. 속도선을 끈다.
    public void StopDashLines()
    {
        if (_dashLinesExit != null)
        {
            _dashLinesExit.PlayFeedbacks();
        }
    }

    // 런으로 벽에 부딪혔을 때의 피드백(카메라 흔들림)을 재생한다.
    public void PlayImpactFeedbacks()
    {
        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.PlayFeedbacks();
        }
    }

    protected override void DrawHitboxGizmos(float facingDirection)
    {
        DrawHitboxGizmo(_bodyHitbox, facingDirection, "대시·런 몸 충돌");
    }
}

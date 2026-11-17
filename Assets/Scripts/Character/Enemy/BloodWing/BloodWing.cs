using MoreMountains.Feedbacks;
using UnityEngine;

// BloodWing 보스. 1페이즈만 있는 공중형 보스다. 중력을 받지 않고 화면 위쪽 양 모서리(대기 위치) 중 한 곳에 떠 있다.
// 소환되면 스폰 지점에서 가까운 쪽 대기 위치에 바로 놓이고 Idle로 시작한다.
// 흐름: Idle → (무작위) 브레스 전조(AttackReady) → 브레스(Attack) → Idle,
//       또는 대시 전조(DashReady) → 대시(Dash, 플레이어 쪽 돌진) → 대시 복귀(DashRecover, 가까운 대기 위치로) → Idle
//  - 브레스(공격): 지금 있는 쪽 대기 위치에서 반대쪽 대기 위치까지 같은 높이로 일정하게 날아가며, 입에서 날아가는 쪽 아래 대각선으로 화염을 뿜는다.
//    화염은 바닥·벽에 닿는 곳까지 뻗는 긴 직사각형 판정이며, 보스 바로 아래(화염 뒤쪽)가 안전하다.
//    플레이어는 맞은 뒤 무적 시간 동안에는 피해를 입지 않고, 무적이 끝났는데도 화염에 닿아 있으면 다시 맞는다.
//  - 대시: 플레이어를 보며 살짝 위로 올라가는 준비 자세(전조) → 준비가 끝나는 순간의 플레이어 위치 쪽으로 빠르게 일직선 돌진(몸통박치기) →
//    벽이나 바닥에 부딪히면 카메라가 흔들리고, 가까운 쪽 대기 위치(같으면 오른쪽)로 돌아가 Idle. 돌진하는 동안에만 몸에 닿은 플레이어가 피해를 입는다. (한 번만)
//    돌진 중 바닥에 남은 운석에 부딪히면 운석이 사라지고 카메라가 흔들리며 그 자리에서 기절(Stun)한 뒤, 가까운 대기 위치로 돌아가 Idle.
//    (기절은 컴포넌트 컨텍스트 메뉴 "Stun (플레이 중)"으로도 확인할 수 있다)
//  - 스킬(메테오): 패턴 게이지는 Skill 상태가 아닐 때 20초에 걸쳐 100%까지 찬다. 꽉 찬 채로 Idle에 들어가면 바로 Skill로 간다. (Idle이 아니면 들어가지 않는다)
//    화면 가운데(대기 위치 높이)로 이동 → 게이지가 10초에 걸쳐 0%까지 줄어드는 동안 두 벽 사이 무작위 위치에 운석이 떨어짐 → 원래 자리로 이동 → Idle.
//    운석 중 하나는 바닥에 남는다. (BloodWingMeteor)
//  - 이동은 모두 정해진 시간 동안 일정한 속도로 가는 직선 이동이다. (BloodWingLinearMove)
//  - 대기 위치·벽·바닥은 전투 시작 때 메인 카메라가 비추는 화면과 지형을 기준으로 한 번 계산한다. (카메라는 고정이다)
// 아직 구현하지 않은 것: Idle 중 대기 위치로 돌아가는 이동(모든 패턴이 대기 위치에서 끝나 지금은 필요 없다).
[RequireComponent(typeof(PatternGauge))]
public class BloodWing : Enemy
{
    private const string GroundLayerName = "Ground";
    private const string WallLayerName = "Wall";
    private const string RemainingMeteorLayerName = "PlayerSkill";
    // 바닥·벽을 화면 가운데에서 찾는 최대 거리
    private const float GroundSearchDistance = 100f;
    // 돌진 중 지형·운석을 찾을 때 한 번에 처리할 최대 결과 수
    private const int MaxDashQueryCount = 4;
    // 돌진하다 지형에 닿을 때 콜라이더 사이에 남기는 간격 (단위: 월드 거리)
    private const float DashContactSkin = 0.02f;
    // 조준점이 벽에 붙을 때, 몸 콜라이더 가장자리와 벽 사이에 남기는 간격 (단위: 월드 거리)
    private const float DashAimWallMargin = 0.05f;
    // 화염이 입 바로 앞에서 지형에 닿아도 판정 박스 크기가 0이 되지 않도록 두는 최소 길이 (단위: 월드 거리)
    private const float MinBreathLength = 0.01f;

    [Header("대기 위치 (화면 위쪽 양 모서리)")]
    [Tooltip("화면 왼쪽·오른쪽 가장자리(벽)에서 대기 위치(보스 중심)까지의 가로 거리입니다. 작을수록 벽에 붙습니다. 대시는 이 위치 바로 아래에서 시작하고 끝나므로, 벽에 바짝 붙은 플레이어도 대시에 맞으려면 1.3보다 작아야 합니다. (대시 몸 판정 반폭 0.7 + 플레이어 피격 판정 폭 0.6) 0.7보다 작으면 몸 콜라이더가 벽에 닿습니다. 대기 중 벽 쪽으로 날개·꼬리가 약 2.1 뻗으므로 2.1보다 작으면 그 부분이 화면 밖으로 잘립니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _perchMarginX = 1f;
    [Tooltip("화면 위쪽 가장자리에서 대기 위치(보스 중심)까지의 세로 거리입니다. 클수록 아래로 내려옵니다. 보스 체력바 UI에 가려지지 않도록 뿔 끝이 체력바보다 아래에 오는 값으로 둡니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _perchMarginY = 3f;

    [Header("패턴")]
    [Tooltip("패턴(브레스, 대시)이 끝난 뒤 다음 패턴을 시전하기 전까지 Idle로 쉬는 시간입니다. 전투 시작 때의 Idle 시간은 '시작' 항목의 Start Idle Duration에서 따로 정합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _patternInterval = 2f;
    [Tooltip("Idle이 끝났을 때 브레스(공격)를 고를 확률입니다. 0이면 항상 대시, 1이면 항상 브레스이고, 나머지 경우는 대시입니다. 기획이 정해지기 전의 임시값입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _breathChance = 0.5f;

    [Header("브레스 (공격)")]
    [Tooltip("브레스를 뿜으며 반대쪽 대기 위치까지 같은 높이로 날아가는 데 걸리는 시간입니다. 일정한 속도로 움직입니다. 전조 시간은 포함하지 않습니다. (전조 시간은 BloodWing_AttackReady 클립 길이입니다) 기획이 정해지기 전의 임시값입니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _breathDuration = 3f;
    [Tooltip("화염이 뻗는 각도입니다. 바로 아래(수직)를 0으로 해서 날아가는 쪽(앞)으로 기울어진 각도입니다. 클수록 앞쪽 멀리 닿고, 보스 아래의 안전한 공간이 넓어집니다. (단위: 도)")]
    [SerializeField, Range(0f, 80f)] private float _breathAngle = 30f;
    [Tooltip("화염이 시작되는 입 위치입니다. 루트 위치 기준이며 오른쪽을 볼 때 기준입니다. (단위: 월드 거리)")]
    [SerializeField] private Vector2 _breathMouthOffset = new Vector2(0.3f, -0.5f);
    [Tooltip("화염 판정(긴 직사각형)의 두께입니다. 화염 그림(그레이박스)도 같은 두께로 그려집니다. (단위: 월드 거리)")]
    [SerializeField, Min(0.01f)] private float _breathWidth = 0.8f;
    [Tooltip("화염의 최대 길이입니다. 바닥이나 벽에 닿으면 그 지점까지만 뻗습니다. (단위: 월드 거리)")]
    [SerializeField, Min(0.01f)] private float _breathMaxLength = 12f;
    [Tooltip("화염에 닿은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 플레이어는 맞은 뒤 무적 시간 동안에는 피해를 입지 않고, 무적이 끝났는데도 화염에 닿아 있으면 다시 맞습니다. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _breathDamageMultiplier = 1f;
    [Tooltip("화염 그림(그레이박스)입니다. Visuals/MirroredVFX 아래의 SpriteRenderer를 연결하며, 기본은 꺼 둡니다. 위치·각도·길이·두께는 위의 값에 맞춰 코드가 정합니다. 스프라이트의 피벗은 가운데(Center)여야 합니다.\n비워 두면 그림 없이 판정만 나갑니다.")]
    [SerializeField] private SpriteRenderer _breathFlame;

    [Header("대시")]
    [Tooltip("대시 준비 자세(전조) 시간입니다. 이 시간 동안 제자리에서 플레이어 쪽을 바라보고, 끝나는 순간의 플레이어 위치로 돌진합니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _dashReadyDuration = 0.5f;
    [Tooltip("준비 자세 동안 위로 올라가는 거리입니다. 준비 시간에 걸쳐 일정하게 올라가고, 돌진은 올라간 자리에서 시작합니다. 0이면 올라가지 않습니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _dashReadyRiseDistance = 0.3f;
    [Tooltip("플레이어 쪽으로 일직선 돌진하는 속도입니다. 벽이나 바닥에 부딪힐 때까지 이 속도로 날아갑니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _dashSpeed = 36f;
    [Tooltip("돌진이 벽이나 바닥에 닿지 않는 예외 상황(위쪽으로 돌진 등)에서 돌진을 끝내는 최대 시간입니다. (단위: 초)")]
    [SerializeField, Min(0.1f)] private float _dashMaxDuration = 2f;
    [Tooltip("돌진이 끝나거나(벽·바닥 충돌) 기절이 풀린 뒤, 가까운 쪽 대기 위치로 일정하게 돌아가는 시간입니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _dashReturnDuration = 0.5f;
    [Tooltip("돌진 중 몸에 닿은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _dashDamageMultiplier = 1f;
    [Tooltip("돌진 중 몸 충돌 판정 범위입니다. 돌진하는 동안에만 이 범위에 닿은 플레이어가 피해를 입고(한 번의 대시에서 한 번만), 이 범위에 바닥에 남은 운석이 닿으면 기절합니다.")]
    [SerializeField] private HitboxShape _dashBodyHitbox = new HitboxShape(Vector2.zero, new Vector2(1.4f, 2.2f));
    [Tooltip("돌진이 벽이나 바닥에 부딪히거나 남은 운석에 부딪혀 기절할 때 재생하는 피드백입니다. (카메라 흔들림, SoulKnight 슬라이딩 충돌과 같은 값) 루트 VFX/Impact/ImpactFeedbacks의 MMF_Player를 연결합니다.")]
    [SerializeField] private MMF_Player _impactFeedbacks;

    [Header("패턴 게이지 · 스킬 (메테오)")]
    [Tooltip("패턴 게이지가 0%에서 100%까지 차는 데 걸리는 시간입니다. Skill 상태가 아닐 때 항상 일정하게 찹니다. 꽉 찬 채로 Idle에 들어가면 바로 Skill로 갑니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _gaugeFillDuration = 20f;
    [Tooltip("Skill 중 패턴 게이지가 100%에서 0%까지 줄어드는 데 걸리는 시간입니다. 이 시간 동안 운석이 떨어지고, 0%가 되면 원래 자리로 돌아갑니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _gaugeDrainDuration = 10f;
    [Tooltip("Skill을 시작할 때 화면 가운데로, 끝날 때 원래 자리로 일정하게 이동하는 시간입니다. 이동하는 동안에는 게이지가 줄지 않고 운석도 떨어지지 않습니다. 기획이 정해지기 전의 임시값입니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _skillMoveDuration = 1f;
    [Tooltip("떨어뜨릴 운석 프리팹입니다. BloodWingMeteor와 Manual 반환 PooledObject를 가진 프리팹을 씁니다.")]
    [SerializeField] private BloodWingMeteor _meteorPrefab;
    [Tooltip("운석이 떨어지는 간격입니다. 짧을수록 많이 떨어집니다. 기획이 정해지기 전의 임시값입니다. (단위: 초)")]
    [SerializeField, Min(0.05f)] private float _meteorInterval = 0.35f;
    [Tooltip("운석 몇 개마다 하나를 플레이어 머리 위로 떨어뜨릴지입니다. 4면 4개 중 1개가 떨어뜨리는 순간의 플레이어 위치로 떨어지고 나머지는 무작위 위치입니다. 벽에 붙어 있어도 맞을 수 있게 합니다. 0이면 조준하지 않습니다. 기획이 정해지기 전의 임시값입니다. (단위: 개)")]
    [SerializeField, Min(0)] private int _aimedMeteorEvery = 4;
    [Tooltip("운석이 떨어지는 속도입니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _meteorSpeed = 9f;
    [Tooltip("운석에 맞은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _meteorDamageMultiplier = 1f;

    [Header("기절")]
    [Tooltip("돌진 중 바닥에 남은 운석에 부딪혀 기절했을 때 그 자리에 멈춰 있는 시간입니다. 기절 모션은 반복하지 않고 한 번 재생한 뒤 마지막 그림을 유지합니다. 끝나면 가까운 대기 위치로 돌아갑니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _stunDuration = 3f;
    [Tooltip("기절하는 동안 머리 위(StatusVfxDisplay의 Anchor)에 띄울 VFX 프리팹입니다. 반복 재생되는 Flipbook과 Manual 반환 PooledObject를 가진 프리팹을 씁니다.\n비워 두면 VFX 없이 기절합니다.")]
    [SerializeField] private GameObject _stunVfxPrefab;

    // 전투 시작 때 계산해 두는 값들. (월드 좌표)
    private Vector2 _leftPerch;
    private Vector2 _rightPerch;
    // 화면 가운데의 x. 지금 어느 쪽 대기 위치에 있는지 가르는 기준이다.
    private float _fieldCenterX;
    // 양쪽 벽(Wall 레이어 콜라이더)의 안쪽 면 x. 운석이 벽과 겹치지 않는 범위를 정한다. 벽을 찾지 못하면 화면 가장자리다.
    private float _leftWallX;
    private float _rightWallX;
    private float _screenTop;
    private float _groundY;
    // 루트 위치에서 몸 콜라이더 아래쪽까지의 세로 거리. 돌진 조준점(바닥에 닿았을 때의 몸 높이)을 정한다.
    private float _bodyBottomOffset;
    // 몸 콜라이더의 반폭. 돌진 조준점이 벽에 끼지 않게 가로 범위를 자르는 데 쓴다.
    private float _bodyHalfWidth;
    // 화염을 막는 지형 (바닥, 벽)
    private int _breathObstacleMask;
    // 돌진을 멈추는 지형(바닥, 벽)과 기절시키는 남은 운석을 찾는 조건
    private ContactFilter2D _dashObstacleFilter;
    private ContactFilter2D _remainingMeteorFilter;
    private readonly RaycastHit2D[] _dashCastResults = new RaycastHit2D[MaxDashQueryCount];
    private readonly Collider2D[] _meteorOverlapResults = new Collider2D[MaxDashQueryCount];

    public BloodWingAttackReadyState AttackReadyState { get; private set; }
    public BloodWingAttackState AttackState { get; private set; }
    public BloodWingDashReadyState DashReadyState { get; private set; }
    public BloodWingDashState DashState { get; private set; }
    public BloodWingDashRecoverState DashRecoverState { get; private set; }
    public BloodWingSkillState SkillState { get; private set; }
    public BloodWingStunState StunState { get; private set; }

    public float PatternInterval => _patternInterval;
    public float GaugeDrainDuration => _gaugeDrainDuration;
    public float SkillMoveDuration => _skillMoveDuration;
    public float MeteorInterval => _meteorInterval;
    public int AimedMeteorEvery => _aimedMeteorEvery;
    public float BreathDuration => _breathDuration;
    public float DashReadyDuration => _dashReadyDuration;
    public float DashReadyRiseDistance => _dashReadyRiseDistance;
    public float DashSpeed => _dashSpeed;
    public float DashMaxDuration => _dashMaxDuration;
    public float DashReturnDuration => _dashReturnDuration;
    public float DashDamage => Stats.AttackPower * _dashDamageMultiplier;
    public HitboxShape DashBodyHitbox => _dashBodyHitbox;
    public float StunDuration => _stunDuration;
    public GameObject StunVfxPrefab => _stunVfxPrefab;

    protected override bool OnInit(BattleContext context)
    {
        if (!base.OnInit(context))
        {
            return false;
        }

        if (!TryCalculateFlightPoints())
        {
            return false;
        }

        _breathObstacleMask = LayerMask.GetMask(GroundLayerName, WallLayerName);
        SetBreathFlameVisible(false);

        _dashObstacleFilter = new ContactFilter2D();
        _dashObstacleFilter.SetLayerMask(_breathObstacleMask);
        _dashObstacleFilter.useTriggers = false;
        _remainingMeteorFilter = new ContactFilter2D();
        _remainingMeteorFilter.SetLayerMask(LayerMask.GetMask(RemainingMeteorLayerName));
        // 남은 운석의 콜라이더는 트리거이므로 트리거도 조회한다.
        _remainingMeteorFilter.useTriggers = true;

        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.Initialization();
        }

        // 게이지가 꽉 찬 것은 "지금 때리면 기절"이 아니라 곧 스킬이 나온다는 뜻이라 꽉 참 강조(UI 튕김·깜빡임)를 끈다.
        PatternGauge.SetFullEmphasisEnabled(false);

        if (_meteorPrefab == null)
        {
            Debug.LogWarning("BloodWing에 운석 프리팹이 없어 스킬 중 운석이 떨어지지 않습니다.", this);
        }
        else if (PoolManager.HasInstance)
        {
            // 첫 스킬 때 생성 비용으로 끊기지 않도록 미리 만들어 둔다.
            PoolManager.Instance.Prewarm(_meteorPrefab.gameObject, 6);
        }

        if (_stunVfxPrefab != null)
        {
            if (StatusVfx == null)
            {
                Debug.LogWarning("BloodWing 루트에 StatusVfxDisplay가 없어 기절 VFX를 띄우지 않습니다.", this);
            }
            else if (PoolManager.HasInstance)
            {
                // 첫 기절 때 생성 비용으로 끊기지 않도록 미리 하나 만들어 둔다.
                PoolManager.Instance.Prewarm(_stunVfxPrefab, 1);
            }
        }

        return true;
    }

    // 전투가 시작될 때 중력을 끄고, 스폰 지점에서 가까운 쪽 대기 위치에 바로 놓은 뒤 화면 안쪽을 본다.
    // 중력은 Release에서 되돌리지 않는다. (Release는 파괴 중에도 호출되어 Rigidbody2D가 이미 없을 수 있고, 다시 Init하면 여기서 다시 끈다)
    protected override void OnInitCompleted()
    {
        base.OnInitCompleted();

        Movement.SetGravityEnabled(false);
        Movement.SetVelocity(Vector2.zero);

        Vector2 perch = transform.position.x >= _fieldCenterX ? _rightPerch : _leftPerch;
        transform.position = new Vector3(perch.x, perch.y, transform.position.z);
        Animation.UpdateFacing(_fieldCenterX - perch.x);
    }

    // 브레스 도중에 소환 해제되면 상태의 Exit가 불리지 않으므로 화염을 여기서 끈다. 충돌 피드백(카메라 흔들림)도 멈춘다.
    protected override void OnRelease()
    {
        SetBreathFlameVisible(false);

        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.StopFeedbacks();
        }

        base.OnRelease();
    }

    protected override void CreateStates()
    {
        base.CreateStates();
        AttackReadyState = new BloodWingAttackReadyState(this, stateMachine);
        AttackState = new BloodWingAttackState(this, stateMachine);
        DashReadyState = new BloodWingDashReadyState(this, stateMachine);
        DashState = new BloodWingDashState(this, stateMachine);
        DashRecoverState = new BloodWingDashRecoverState(this, stateMachine);
        SkillState = new BloodWingSkillState(this, stateMachine);
        StunState = new BloodWingStunState(this, stateMachine);
    }

    // Idle 시간이 끝났을 때 호출된다. (플레이어가 죽어 있는 동안에는 호출되지 않는다)
    // 브레스와 대시 중 하나를 Breath Chance 확률로 골라 전조부터 시전한다.
    public override void ChangeToNextPattern()
    {
        if (Random.value < _breathChance)
        {
            stateMachine.ChangeState(AttackReadyState);
        }
        else
        {
            stateMachine.ChangeState(DashReadyState);
        }
    }

    // Idle 중 매 프레임 호출된다. 패턴 게이지가 꽉 찼으면 Idle 시간을 기다리지 않고 바로 Skill로 간다.
    // (Idle이 아닌 상태에서는 꽉 차도 들어가지 않고, 그 패턴이 끝나 Idle에 들어갈 때 들어간다)
    public override bool TryInterruptIdle()
    {
        if (!PatternGauge.IsFull)
        {
            return false;
        }

        stateMachine.ChangeState(SkillState);
        return true;
    }

    // 패턴 게이지를 채운다. Skill 상태가 아니면 항상 Gauge Fill Duration에 걸쳐 0%에서 100%까지 일정하게 찬다. 죽은 뒤에는 차지 않는다.
    protected override void OnUpdate()
    {
        if (Vital.IsDead || CurrentState == SkillState)
        {
            return;
        }

        PatternGauge.Add(PatternGauge.MaxValue / _gaugeFillDuration * Time.deltaTime);
    }

    // Skill 중 패턴 게이지를 deltaTime만큼 줄인다. Gauge Drain Duration에 걸쳐 100%에서 0%까지 일정하게 준다. 0%가 되면 true를 반환한다.
    public bool DrainGauge(float deltaTime)
    {
        PatternGauge.Reduce(PatternGauge.MaxValue / _gaugeDrainDuration * deltaTime);
        return PatternGauge.CurrentValue <= 0f;
    }

    // Skill 때 머무는 자리. 화면 가로 가운데, 높이는 대기 위치와 같다.
    public Vector2 GetSkillPoint()
    {
        return new Vector2(_fieldCenterX, _leftPerch.y);
    }

    // 운석 하나를 화면 위쪽(화면 밖 바로 위)에서 떨어뜨린다. remainsOnGround면 바닥에 남는 운석이다.
    // aimAtTarget이면 지금 플레이어의 가로 위치로, 아니면 두 벽 사이 무작위 가로 위치로 떨어진다.
    // 어느 쪽이든 운석이 벽 콜라이더와 겹치지 않는 범위로 자른다. (벽에 바짝 붙은 플레이어도 조준 운석에는 맞는다)
    public void DropMeteor(bool remainsOnGround, bool aimAtTarget)
    {
        if (_meteorPrefab == null || !PoolManager.HasInstance)
        {
            return;
        }

        GameObject instance = PoolManager.Instance.Spawn(_meteorPrefab.gameObject, new Vector3(_fieldCenterX, _screenTop, 0f), Quaternion.identity);
        BloodWingMeteor meteor = instance.GetComponent<BloodWingMeteor>();

        if (meteor == null)
        {
            Debug.LogError("운석 프리팹에 BloodWingMeteor가 없습니다.", this);
            PoolManager.Instance.Despawn(instance);
            return;
        }

        float radius = meteor.Radius;
        float minX = _leftWallX + radius;
        float maxX = Mathf.Max(minX, _rightWallX - radius);
        float x = aimAtTarget && !IsTargetDead ? Target.transform.position.x : Random.Range(minX, maxX);
        Vector2 start = new Vector2(Mathf.Clamp(x, minX, maxX), _screenTop + radius);
        meteor.Fall(start, _meteorSpeed, _groundY, Stats.AttackPower * _meteorDamageMultiplier, gameObject, remainsOnGround);
    }

    // 브레스 화염을 한 번 처리한다. 바닥·벽까지의 길이를 재고, 그 범위에 판정을 내고, 화염 그림을 맞춘다. 브레스 중 매 물리 프레임 호출한다.
    // 맞은 대상 기록을 매번 비운다. 그래서 플레이어가 맞은 뒤 무적 시간이 끝났는데도 화염에 닿아 있으면 다시 맞는다. (무적 중에는 CharacterVital이 피해를 막는다)
    // facingDirection: 보는(날아가는) 방향 (오른쪽 1, 왼쪽 -1)
    public void UpdateBreath(float facingDirection)
    {
        Vector2 direction = GetBreathDirection(facingDirection);
        Vector2 origin = GetBreathOrigin(Movement.Position, facingDirection);
        float length = MeasureBreathLength(origin, direction, _breathObstacleMask);
        Vector2 center = origin + direction * (length * 0.5f);
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        HitChecker.ResetHitTargets();
        HitChecker.Hit(center, new Vector2(length, _breathWidth), angle, Stats.AttackPower * _breathDamageMultiplier);
        UpdateBreathFlame(length, facingDirection);
    }

    // 화염 그림(그레이박스)을 켜고 끈다.
    public void SetBreathFlameVisible(bool isVisible)
    {
        if (_breathFlame != null)
        {
            _breathFlame.gameObject.SetActive(isVisible);
        }
    }

    // 지금 위치(x)의 반대쪽 대기 위치. 브레스와 수평 대시가 향하는 쪽이다.
    public Vector2 GetOppositePerch(float positionX)
    {
        return positionX >= _fieldCenterX ? _leftPerch : _rightPerch;
    }

    // 화면 안쪽(가운데)을 본다. 대기 위치에 도착했을 때 쓴다.
    public void FaceFieldCenter()
    {
        Animation.UpdateFacing(_fieldCenterX - Movement.Position.x);
    }

    // 지금 위치(x)에서 가까운 쪽 대기 위치. 두 대기 위치와의 거리가 같으면(화면 가운데) 오른쪽이다. 대시·기절이 끝나고 돌아갈 지점이다.
    public Vector2 GetNearestPerch(float positionX)
    {
        return positionX >= _fieldCenterX ? _rightPerch : _leftPerch;
    }

    // 돌진이 향할 지점. 플레이어의 가로 위치에서, 몸이 바닥에 닿았을 때의 높이(플레이어가 그보다 높이 떠 있으면 플레이어 높이)다.
    // 플레이어 중심을 그대로 겨누면 보스 몸이 키가 커서 플레이어보다 앞에서 바닥에 먼저 닿으므로, 바닥에 닿는 자리가 플레이어 위치가 되게 한다.
    // 플레이어가 없으면(죽음) 지금 자리 바로 아래다.
    // 가로 위치는 몸이 벽에 끼지 않는 범위로 자른다. 벽에 바짝 붙은 플레이어를 그대로 겨누면 몸(폭 1.4)이 바닥에 닿기 전에 벽에 먼저 부딪혀 멈추므로,
    // 몸이 벽에 닿기 직전까지만 겨눠도 몸 판정이 벽에 붙은 플레이어와 겹친다.
    public Vector2 GetDashAimPoint()
    {
        float restY = _groundY + _bodyBottomOffset;
        float minX = _leftWallX + _bodyHalfWidth + DashAimWallMargin;
        float maxX = Mathf.Max(minX, _rightWallX - _bodyHalfWidth - DashAimWallMargin);

        if (IsTargetDead)
        {
            return new Vector2(Mathf.Clamp(Movement.Position.x, minX, maxX), restY);
        }

        Vector2 targetPosition = Target.transform.position;
        return new Vector2(Mathf.Clamp(targetPosition.x, minX, maxX), Mathf.Max(targetPosition.y, restY));
    }

    // 몸 콜라이더를 direction(단위 벡터)으로 distance만큼 옮길 때 벽·바닥에 닿는지 확인한다.
    // 닿으면 true와 함께, 닿기 직전까지 옮길 수 있는 거리를 freeDistance로 준다. 안 닿으면 freeDistance는 distance다.
    public bool CastBody(Vector2 direction, float distance, out float freeDistance)
    {
        int hitCount = BodyCollider.Cast(direction, _dashObstacleFilter, _dashCastResults, distance + DashContactSkin);
        freeDistance = distance;

        if (hitCount == 0)
        {
            return false;
        }

        float nearest = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            nearest = Mathf.Min(nearest, _dashCastResults[i].distance);
        }

        freeDistance = Mathf.Clamp(nearest - DashContactSkin, 0f, distance);
        return true;
    }

    // 돌진 몸 판정 범위에 닿은 바닥에 남은 운석을 찾는다. 있으면 true와 함께 그 운석을 준다.
    // facingDirection: 보는 방향 (오른쪽 1, 왼쪽 -1)
    public bool TryFindRemainingMeteor(float facingDirection, out BloodWingMeteor meteor)
    {
        Vector2 center = _dashBodyHitbox.GetCenter(Movement.Position, facingDirection);
        int overlapCount = Physics2D.OverlapBox(center, _dashBodyHitbox.Size, 0f, _remainingMeteorFilter, _meteorOverlapResults);

        for (int i = 0; i < overlapCount; i++)
        {
            if (_meteorOverlapResults[i].TryGetComponent(out meteor) && meteor.IsLanded)
            {
                return true;
            }
        }

        meteor = null;
        return false;
    }

    // 돌진이 벽·바닥이나 남은 운석에 부딪혔을 때의 피드백(카메라 흔들림)을 재생한다.
    public void PlayImpactFeedbacks()
    {
        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.PlayFeedbacks();
        }
    }

    // 기절 상태로 바꾼다. 죽었거나 페이즈 전환 중이면 아무것도 하지 않는다.
    // 플레이 중 컴포넌트 컨텍스트 메뉴로 기절을 확인한다.
    [ContextMenu("Stun (플레이 중)")]
    private void PreviewStun()
    {
        ChangeToStun();
    }

    // 돌진 중 바닥에 남은 운석에 부딪혔을 때 BloodWingDashState가 호출한다. 기절 상태로 바꿨으면 true를 반환한다.
    public bool ChangeToStun()
    {
        if (!IsInitialized || Vital.IsDead || IsPhaseTransitioning)
        {
            return false;
        }

        stateMachine.ChangeState(StunState);
        return true;
    }

    protected override void DrawHitboxGizmos(float facingDirection)
    {
        DrawHitboxGizmo(_dashBodyHitbox, facingDirection, "대시 몸 충돌");

        // 화염은 지금 자리에서 바닥·벽까지 뻗은 길이로 그린다. (프리팹 편집 화면처럼 지형이 없으면 최대 길이)
        Vector2 direction = GetBreathDirection(facingDirection);
        Vector2 origin = GetBreathOrigin(transform.position, facingDirection);
        float length = MeasureBreathLength(origin, direction, LayerMask.GetMask(GroundLayerName, WallLayerName));
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        DrawHitboxGizmo(origin + direction * (length * 0.5f), new Vector2(length, _breathWidth), angle, "브레스 화염");
    }

    // 화염이 뻗는 방향(단위 벡터). 바로 아래에서 보는 방향 쪽으로 Breath Angle만큼 기울어진다.
    private Vector2 GetBreathDirection(float facingDirection)
    {
        float radian = _breathAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Sin(radian) * facingDirection, -Mathf.Cos(radian));
    }

    // 화염이 시작되는 입의 월드 위치
    private Vector2 GetBreathOrigin(Vector2 rootPosition, float facingDirection)
    {
        return rootPosition + new Vector2(_breathMouthOffset.x * facingDirection, _breathMouthOffset.y);
    }

    // 입에서 화염 방향으로 바닥·벽에 닿는 곳까지의 길이. 닿는 지형이 없으면 최대 길이다.
    private float MeasureBreathLength(Vector2 origin, Vector2 direction, int obstacleMask)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, _breathMaxLength, obstacleMask);
        return hit.collider != null ? Mathf.Max(hit.distance, MinBreathLength) : _breathMaxLength;
    }

    // 화염 그림을 입에서 length만큼 뻗은 긴 직사각형으로 맞춘다.
    // 그림은 좌우 반전을 맡는 Visuals 아래에 있으므로 각도는 오른쪽을 볼 때 기준으로 정하고, 왼쪽은 부모의 반전에 맡긴다.
    private void UpdateBreathFlame(float length, float facingDirection)
    {
        if (_breathFlame == null || _breathFlame.sprite == null || _breathFlame.transform.parent == null)
        {
            return;
        }

        Transform flameTransform = _breathFlame.transform;
        Transform parent = flameTransform.parent;
        Vector2 localDirection = GetBreathDirection(1f);
        Vector2 localCenter = _breathMouthOffset + localDirection * (length * 0.5f);
        // 루트 기준 위치를 부모 기준 로컬 값으로 바꿔 넣으므로, 그림은 부모(Visuals)와 함께 움직이고 Rigidbody 보간과도 어긋나지 않는다.
        Vector3 worldCenter = transform.TransformPoint(new Vector3(localCenter.x * facingDirection, localCenter.y, 0f));
        Vector3 parentScale = parent.lossyScale;
        Vector3 spriteSize = _breathFlame.sprite.bounds.size;

        flameTransform.localPosition = parent.InverseTransformPoint(worldCenter);
        flameTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(localDirection.y, localDirection.x) * Mathf.Rad2Deg);
        flameTransform.localScale = new Vector3(length / Mathf.Abs(parentScale.x) / spriteSize.x, _breathWidth / Mathf.Abs(parentScale.y) / spriteSize.y, 1f);
    }

    // origin에서 direction(왼쪽 -1, 오른쪽 1)으로 가장 가까운 벽(Wall 레이어)의 안쪽 면 x. 벽이 없으면 fallbackX를 반환한다.
    private float FindWallInnerX(Vector2 origin, float direction, float fallbackX)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(direction, 0f), GroundSearchDistance, LayerMask.GetMask(WallLayerName));
        return hit.collider != null ? hit.point.x : fallbackX;
    }

    // 메인 카메라가 비추는 화면과 그 아래 바닥을 기준으로 양쪽 대기 위치와 바닥 높이를 계산한다.
    // 기준을 찾지 못하면 false를 반환한다. (Init 실패)
    private bool TryCalculateFlightPoints()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError($"{name}: 메인 카메라(MainCamera 태그)가 없어 대기 위치를 정할 수 없습니다.", this);
            return false;
        }

        Vector2 cameraCenter = mainCamera.transform.position;
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        float screenTop = cameraCenter.y + halfHeight;

        Vector2 groundSearchOrigin = new Vector2(cameraCenter.x, screenTop);
        RaycastHit2D hit = Physics2D.Raycast(groundSearchOrigin, Vector2.down, GroundSearchDistance, LayerMask.GetMask(GroundLayerName));

        if (hit.collider == null)
        {
            Debug.LogError($"{name}: 화면 가운데 아래에서 바닥({GroundLayerName} 레이어)을 찾지 못해 운석이 떨어질 바닥 높이를 정할 수 없습니다.", this);
            return false;
        }

        float perchOffsetX = Mathf.Max(0f, halfWidth - _perchMarginX);
        float perchY = screenTop - _perchMarginY;

        _fieldCenterX = cameraCenter.x;
        _screenTop = screenTop;
        _leftWallX = FindWallInnerX(cameraCenter, -1f, cameraCenter.x - halfWidth);
        _rightWallX = FindWallInnerX(cameraCenter, 1f, cameraCenter.x + halfWidth);
        _leftPerch = new Vector2(cameraCenter.x - perchOffsetX, perchY);
        _rightPerch = new Vector2(cameraCenter.x + perchOffsetX, perchY);
        _groundY = hit.point.y;
        _bodyBottomOffset = transform.position.y - BodyCollider.bounds.min.y;
        _bodyHalfWidth = BodyCollider.bounds.extents.x;
        return true;
    }
}

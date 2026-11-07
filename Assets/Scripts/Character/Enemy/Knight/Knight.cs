using UnityEngine;
using UnityEngine.Serialization;

// 첫 번째 보스 Knight(임시 이름). GDD 6.6의 상태 구성과 전환 규칙을 따른다.
// 패턴 상태 인스턴스와 패턴 수치를 보관하고, 다음 패턴을 고르는 규칙(사거리 안: 공격 1회/2회, 사거리 밖: 점프/벽타기)을 담당한다.
// 패턴이 하나 끝나면 기본적으로 Idle로 쉬어간다. Idle 없이 이어지는 예외는 세 가지뿐이다.
// (공격 1회/2회 뒤 랜덤 구르기, 구르기 뒤 공격 1회/2회, 점프 뒤 공격 1회/2회) 구르기 직후의 공격 뒤에는 구르기를 하지 않는다.
// 패턴 게이지(지침)가 꽉 찬 채로 패턴이 끝나면 위 규칙 대신 기절한다. 기절이 끝나면 일어나기를 거쳐 항상 Idle로 쉬어간다.
// 각 패턴의 진행과 종료 판단은 Knight/States/의 상태가 담당한다.
// 수치는 기획에서 정해지지 않은 임시값이며 플레이하면서 인스펙터에서 조정한다. (GDD 9장)
[RequireComponent(typeof(PatternGauge))]
public class Knight : Enemy
{
    [Header("패턴 선택")]
    [Tooltip("플레이어와의 가로 거리가 이 값 이하이면 사거리 안으로 봅니다.\n사거리 안이면 공격 1회/2회, 사거리 밖이면 점프/벽타기를 고릅니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _attackRange = 3f;
    [Tooltip("사거리 안에서 공격 2회(콤보)를 고를 확률입니다. 나머지 확률은 공격 1회입니다.\n0 = 항상 공격 1회, 1 = 항상 공격 2회")]
    [SerializeField, Range(0f, 1f)] private float _comboChance = 0.5f;
    [Tooltip("사거리 밖에서 벽타기를 고를 확률입니다. 나머지 확률은 점프입니다.\n0 = 항상 점프, 1 = 항상 벽타기")]
    [SerializeField, Range(0f, 1f)] private float _wallClimbChance = 0.5f;
    [Tooltip("공격 1회/2회가 끝난 뒤 Idle 없이 바로 구르기를 시전할 확률입니다. 구르기를 하지 않으면 Idle로 갑니다.\n구르기 직후에 이어진 공격 뒤에는 이 확률과 관계없이 구르기를 하지 않습니다.")]
    [SerializeField, Range(0f, 1f)] private float _rollChance = 0.5f;
    [Tooltip("패턴이 하나 끝난 뒤 Idle로 쉬어가는 시간입니다. 쉬는 동안 플레이어가 공격할 틈이 생깁니다. (단위: 초)\nIdle 없이 이어지는 경우(공격 뒤 구르기, 구르기 뒤 공격, 점프 뒤 공격)에는 쉬지 않습니다.")]
    [SerializeField, Min(0f)] private float _idleDuration = 1f;

    // 공격 클립(Knight_Attack, Knight_AttackCombo)은 칼을 휘두르기 전 준비 프레임을 0.3초 멈춰 두고 휘두름만 12fps로 재생한다.
    // 휘두름의 시작 시점은 클립의 휘두름 시작에 맞춘 값이다. 클립의 프레임 시간을 바꾸면 함께 맞춰야 한다.
    [Header("공격 (공통)")]
    [Tooltip("공격 1회/2회를 하면서 플레이어 쪽으로 전진하는 가로 속도입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _attackStepSpeed = 3f;

    [Header("공격 1회")]
    [Tooltip("공격 1회의 휘두름 목록입니다. (1개) 휘두름마다 시작·끝 시점, 피해 배율, 전진 길이, 판정 범위를 정합니다.\n판정 구간(0.545~0.848)은 휘두름 프레임에 맞춘 값이며, 피해량은 임시값입니다.")]
    [SerializeField] private KnightSwing[] _attackSwings =
    {
        new KnightSwing(0.545f, 0.848f, 1f, 0.25f, new HitboxShape(new Vector2(2.1f, 0.15f), new Vector2(4.2f, 2.8f)))
    };

    [Header("공격 2회")]
    [Tooltip("공격 2회의 휘두름 목록입니다. (2개, 시작 시점이 빠른 순서) 휘두름마다 따로 판정되므로 한 번의 공격 2회에서 최대 두 번 맞을 수 있습니다.\n판정 구간(0.237~0.368, 0.737~0.868)은 휘두름 프레임에 맞춘 값이며, 피해량은 임시값입니다. 공격 2회 클립이 더 길어서 전진 길이가 공격 1회보다 짧습니다.")]
    [SerializeField] private KnightSwing[] _comboSwings =
    {
        new KnightSwing(0.237f, 0.368f, 0.7f, 0.1f, new HitboxShape(new Vector2(2.1f, 0.15f), new Vector2(4.2f, 2.8f))),
        new KnightSwing(0.737f, 0.868f, 0.7f, 0.1f, new HitboxShape(new Vector2(2.1f, 0.15f), new Vector2(4.2f, 2.8f)))
    };

    [Header("구르기")]
    [Tooltip("구르기 중 이동하는 가로 속도입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _rollSpeed = 6f;

    [Header("점프")]
    [Tooltip("목표(플레이어, 벽)를 향해 점프할 때 가로 속도의 최대값입니다. 목표가 멀어도 이 속도를 넘지 않습니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _jumpMaxSpeedX = 10f;

    [Header("벽타기")]
    [Tooltip("벽타기에서 낮게(바닥 높이에) 매달릴 확률입니다. 낮게 매달리면 슬라이딩(바닥 미끄러짐), 높게 매달리면 드롭킥(대각선 낙하)으로 이어집니다.\n0 = 항상 높게(드롭킥), 1 = 항상 낮게(슬라이딩)")]
    [SerializeField, Range(0f, 1f)] private float _lowWallHangChance = 0.5f;
    [Tooltip("높게 매달리는 높이입니다. Knight가 벽의 이 높이를 향해 점프해서 매달립니다. (단위: 월드 Y, Knight 위치 기준)\n바닥과 너무 가까우면 매달리자마자 접지로 판정될 수 있으므로 바닥에서 충분히 높게 둡니다. 낮게 매달릴 때는 이 값과 관계없이 바닥 높이에 매달립니다.")]
    [SerializeField] private float _wallHangHeight = 1.5f;
    [Tooltip("벽에 닿은 뒤 매달리는 높이와의 남은 오차를 벽을 타며 맞추는 속도입니다. 높게 매달릴 때는 점프가 매달리는 지점을 향하므로 거의 움직이지 않고, 낮게 매달릴 때는 작은 점프로 벽에 닿은 뒤 이 속도로 바닥까지 내려옵니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _wallClimbSpeed = 15f;
    [Tooltip("벽에 매달려 있는 시간입니다. 높게/낮게 매달릴 때 모두 같습니다. 이 시간이 지나면 드롭킥(높게) 또는 슬라이딩(낮게)으로 이어집니다.\n높게 매달리면 매달리는 순간의 플레이어 위치를 드롭킥 낙하 지점으로 정합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _wallHangDuration = 1f;

    [Header("드롭킥 (대각선 낙하)")]
    [Tooltip("드롭킥으로 벽에서 낙하 지점 쪽으로 대각선으로 날아가는 속도입니다. 클수록 빠르게 떨어집니다. (단위: 월드 거리/초)\n슬라이딩(바닥 미끄러짐)도 이 속도로 미끄러집니다.")]
    [FormerlySerializedAs("_slideSpeed")]
    [SerializeField, Min(0f)] private float _dropKickSpeed = 20f;
    [Tooltip("드롭킥 낙하 지점이 플레이어에게서 Knight 쪽으로 얼마나 앞에 있는지입니다. 플레이어가 아니라 그 앞쪽 바닥에 떨어집니다. (단위: 월드 거리)")]
    [FormerlySerializedAs("_slideLandingOffset")]
    [SerializeField, Min(0f)] private float _dropKickLandingOffset = 1.5f;
    [Tooltip("드롭킥 낙하가 이 시간을 넘기면 바닥에 닿지 않았어도 그 자리에서 멈추고 충격을 냅니다. 예외 상황(어딘가에 걸린 경우) 대비용입니다. (단위: 초)")]
    [FormerlySerializedAs("_slideMaxDuration")]
    [SerializeField, Min(0.1f)] private float _dropKickMaxDuration = 1.5f;

    [Header("드롭킥 충격")]
    [Tooltip("드롭킥이 바닥에 닿을 때 한 번 터지는 충격의 판정 범위입니다.\n오프셋 x는 날아간 방향을 기준으로 하며, 가로가 더 긴 직사각형으로 설정합니다.")]
    [FormerlySerializedAs("_slideImpactHitbox")]
    [SerializeField] private HitboxShape _dropKickImpactHitbox = new HitboxShape(new Vector2(2f, 0f), new Vector2(5f, 2.5f));
    [Tooltip("드롭킥 충격의 피해량 배율입니다. 실제 피해 = Knight 공격력 × 이 값입니다.\n슬라이딩 중 몸에 닿을 때도 같은 피해를 줍니다. 1 = 공격력 그대로")]
    [FormerlySerializedAs("_slideImpactDamageMultiplier")]
    [SerializeField, Min(0f)] private float _dropKickDamageMultiplier = 1f;
    [Tooltip("드롭킥 충격이 터질 때 소환할 이펙트 프리팹입니다. 풀(PoolManager)에서 꺼내며, 비워 두면 이펙트 없이 판정만 합니다.")]
    [FormerlySerializedAs("_slideImpactEffectPrefab")]
    [SerializeField] private GameObject _dropKickImpactEffectPrefab;
    [Tooltip("드롭킥 충격 이펙트를 띄울 위치입니다. Knight 위치 기준 오프셋이며, 날아간 방향이 오른쪽일 때 기준입니다. 왼쪽이면 x를 뒤집습니다. (단위: 월드 거리)\n이펙트 그림은 아래쪽에 모여 있으므로 판정 범위의 중심보다 y를 조금 높게 둡니다. 값을 줄이면 이펙트가 바닥 쪽으로 내려갑니다.")]
    [FormerlySerializedAs("_slideImpactEffectOffset")]
    [SerializeField] private Vector2 _dropKickImpactEffectOffset = new Vector2(2f, 0.4f);
    [Tooltip("드롭킥이 바닥에 부딪힐 때 재생할 효과음 데이터입니다. SoundManager의 Sfx 채널로 재생하며, 비워 두면 소리 없이 진행합니다.")]
    [FormerlySerializedAs("_slideImpactSound")]
    [SerializeField] private SoundDataSO _dropKickImpactSound;

    [Header("슬라이딩 (바닥 미끄러짐)")]
    [Tooltip("슬라이딩 중 플레이어가 Knight 몸에 닿았는지 확인하는 판정 범위입니다. 누워서 미끄러지는 모습(Slide 클립의 불투명 영역)에 맞춰 가로로 긴 직사각형입니다.\nKnight 위치 기준이며 오프셋 x는 미끄러지는 방향 기준입니다. (오른쪽으로 갈 때 기준, 왼쪽이면 x를 뒤집음) 한 번의 슬라이딩에서 한 번만 맞습니다. 속도와 피해량은 드롭킥 값을 그대로 씁니다.")]
    [SerializeField] private HitboxShape _slideBodyHitbox = new HitboxShape(new Vector2(0.6f, -0.65f), new Vector2(2.5f, 1.2f));

    // 패턴 게이지는 Knight가 지친 정도다. 패턴을 시전할 때마다 차고, 공격으로 플레이어에게 피해를 입히면 준다.
    // 꽉 찬 채로 패턴이 끝나면 다음 패턴 대신 기절한다. (진행 중인 패턴은 끝까지 한다.) 양은 게이지 최대값(PatternGauge) 기준이다.
    [Header("패턴 게이지 (지침)")]
    [Tooltip("칼을 한 번 휘두를 때마다 게이지가 차는 양입니다. (최대값 100 기준이면 퍼센트)\n휘두르기 시작하는 시점에 채워지므로 공격 1회는 이 값만큼, 공격 2회는 휘두름마다 따로 차서 두 배만큼 찹니다.")]
    [FormerlySerializedAs("_attackGaugeGain")]
    [SerializeField, Min(0f)] private float _swingGaugeGain = 10f;
    [Tooltip("드롭킥(벽에서 대각선 낙하) 또는 슬라이딩(바닥 미끄러짐)을 시작할 때 게이지가 차는 양입니다. (최대값 100 기준이면 퍼센트)")]
    [FormerlySerializedAs("_slideGaugeGain")]
    [SerializeField, Min(0f)] private float _dropKickGaugeGain = 15f;
    [Tooltip("구르기를 시전할 때 게이지가 차는 양입니다. (최대값 100 기준이면 퍼센트)")]
    [SerializeField, Min(0f)] private float _rollGaugeGain = 5f;
    [Tooltip("점프를 시전할 때 게이지가 차는 양입니다. 벽타기로 벽에 뛰어오르는 점프는 포함하지 않습니다. (최대값 100 기준이면 퍼센트)")]
    [SerializeField, Min(0f)] private float _jumpGaugeGain = 5f;
    [Tooltip("공격 1회/2회, 드롭킥 충격, 슬라이딩 몸 충돌이 플레이어에게 피해를 입힐 때마다 게이지가 줄어드는 양입니다. (최대값 100 기준이면 퍼센트)\n공격 2회는 휘두름마다 따로 맞으므로 두 번 맞히면 두 번 줄어듭니다.")]
    [SerializeField, Min(0f)] private float _hitGaugeReduction = 20f;

    [Header("기절")]
    [Tooltip("게이지가 꽉 차서 기절했을 때 멈춰 있는 시간입니다. 이 시간 동안 게이지가 서서히 줄어 끝날 때 0이 됩니다. (단위: 초)\n기절 애니메이션(Death 클립)보다 길면 마지막 프레임을 유지합니다.")]
    [SerializeField, Min(0f)] private float _stunDuration = 3f;
    [Tooltip("기절하는 동안 머리 위(StatusVfxDisplay의 Anchor)에 띄울 VFX 프리팹입니다. 반복 재생되는 Flipbook과 Manual 반환 PooledObject를 가진 프리팹을 씁니다.\n비워 두면 VFX 없이 기절합니다.")]
    [SerializeField] private GameObject _stunVfxPrefab;

    // 바닥을 찾을 때 아래로 검사하는 최대 거리
    private const float GroundSearchDistance = 100f;

    // 바닥 높이를 찾을 때(드롭킥 낙하 지점, 낮게 매달리는 높이) 쓰는 Ground 레이어 마스크
    private int _groundLayerMask;
    // 부모의 Animation 프로퍼티와 이름이 겹치지 않도록 별도 필드에 보관한다.
    private KnightAnimation _knightAnimation;
    // 지금 시전 중인 공격 1회/2회가 끝난 뒤 구르기를 할 수 있는지. (ChangeToAttackPattern에서 정한다.)
    private bool _canRollAfterAttack;

    public new KnightAnimation Animation => _knightAnimation;

    public KnightAttackState AttackState { get; private set; }
    public KnightComboState ComboState { get; private set; }
    public KnightRollState RollState { get; private set; }
    public KnightJumpState JumpState { get; private set; }
    public KnightWallClimbState WallClimbState { get; private set; }
    public KnightDropKickState DropKickState { get; private set; }
    public KnightSlideState SlideState { get; private set; }
    public KnightStunState StunState { get; private set; }
    public KnightStunRecoverState StunRecoverState { get; private set; }

    public float SwingGaugeGain => _swingGaugeGain;
    public float DropKickGaugeGain => _dropKickGaugeGain;
    public float RollGaugeGain => _rollGaugeGain;
    public float JumpGaugeGain => _jumpGaugeGain;
    public float StunDuration => _stunDuration;
    public GameObject StunVfxPrefab => _stunVfxPrefab;

    public float AttackStepSpeed => _attackStepSpeed;
    public KnightSwing[] AttackSwings => _attackSwings;
    public KnightSwing[] ComboSwings => _comboSwings;
    public float RollSpeed => _rollSpeed;
    public float JumpMaxSpeedX => _jumpMaxSpeedX;
    public float LowWallHangChance => _lowWallHangChance;
    public float WallHangHeight => _wallHangHeight;
    public float WallClimbSpeed => _wallClimbSpeed;
    public float WallHangDuration => _wallHangDuration;
    public float DropKickSpeed => _dropKickSpeed;
    public float DropKickMaxDuration => _dropKickMaxDuration;
    public HitboxShape DropKickImpactHitbox => _dropKickImpactHitbox;
    // 드롭킥 충격과 슬라이딩 몸 충돌이 함께 쓰는 피해량
    public float DropKickDamage => Stats.AttackPower * _dropKickDamageMultiplier;
    public HitboxShape SlideBodyHitbox => _slideBodyHitbox;
    // 높게 매달리는 순간 포착한 드롭킥 낙하 지점(월드 좌표). WallClimb가 정하고 DropKick이 쓴다.
    public Vector2 DropKickTargetPosition { get; private set; }

    public bool IsTargetInRange => DistanceToTarget <= _attackRange;

    protected override bool OnInit(BattleContext context)
    {
        if (!base.OnInit(context))
        {
            return false;
        }

        _knightAnimation = GetComponentInChildren<KnightAnimation>();
        _groundLayerMask = LayerMask.GetMask("Ground");

        if (PatternGauge == null)
        {
            Debug.LogError("Knight 루트에 PatternGauge가 없습니다. Knight는 패턴 게이지로 기절을 판단합니다.", this);
            return false;
        }

        if (_knightAnimation == null)
        {
            Debug.LogError("Knight의 Visuals에 KnightAnimation이 없습니다.", this);
            return false;
        }

        if (_attackSwings.Length == 0 || _comboSwings.Length == 0)
        {
            Debug.LogError("Knight의 공격 1회/2회 휘두름 목록이 비어 있습니다. 비어 있으면 공격해도 판정이 나가지 않습니다.", this);
        }

        if (_stunVfxPrefab != null)
        {
            if (StatusVfx == null)
            {
                Debug.LogWarning("Knight 루트에 StatusVfxDisplay가 없어 기절 VFX를 띄우지 않습니다.", this);
            }
            else if (PoolManager.HasInstance)
            {
                // 첫 기절 때 생성 비용으로 끊기지 않도록 미리 하나 만들어 둔다.
                PoolManager.Instance.Prewarm(_stunVfxPrefab, 1);
            }
        }

        return true;
    }

    protected override void CreateStates()
    {
        base.CreateStates();
        AttackState = new KnightAttackState(this, stateMachine);
        ComboState = new KnightComboState(this, stateMachine);
        RollState = new KnightRollState(this, stateMachine);
        JumpState = new KnightJumpState(this, stateMachine);
        WallClimbState = new KnightWallClimbState(this, stateMachine);
        DropKickState = new KnightDropKickState(this, stateMachine);
        SlideState = new KnightSlideState(this, stateMachine);
        StunState = new KnightStunState(this, stateMachine);
        StunRecoverState = new KnightStunRecoverState(this, stateMachine);
    }

    // 사거리 안이면 공격 1회/2회, 사거리 밖이면 점프/벽타기를 고른다.
    public override void ChangeToNextPattern()
    {
        if (IsTargetDead)
        {
            ChangeToIdle(_idleDuration);
            return;
        }

        if (IsTargetInRange)
        {
            ChangeToAttackPattern(true);
            return;
        }

        stateMachine.ChangeState(Random.value < _wallClimbChance ? WallClimbState : JumpState);
    }

    // 공격 1회 또는 공격 2회를 고른다. (사거리 안일 때, 점프 직후, 구르기 직후)
    // canRollAfterAttack: 이 공격이 끝난 뒤 구르기를 시전할 수 있는지. 구르기 직후의 공격은 false여서 구르기가 연달아 이어지지 않는다.
    public void ChangeToAttackPattern(bool canRollAfterAttack)
    {
        if (TryChangeToStun())
        {
            return;
        }

        _canRollAfterAttack = canRollAfterAttack;
        stateMachine.ChangeState(Random.value < _comboChance ? ComboState : AttackState);
    }

    // 공격 1회/2회가 끝났을 때: 구르기가 가능하면 일정 확률로 Idle 없이 바로 구르기를 시전하고, 아니면 Idle로 간다.
    public void ChangeToPatternAfterAttack()
    {
        if (TryChangeToStun())
        {
            return;
        }

        if (_canRollAfterAttack && Random.value < _rollChance)
        {
            stateMachine.ChangeState(RollState);
            return;
        }

        ChangeToRest();
    }

    // 높게 매달리는 순간 호출한다. 그 시점의 플레이어 위치를 포착해 드롭킥 낙하 지점을 정한다.
    // 낙하 지점: 플레이어에게서 Knight 쪽으로 _dropKickLandingOffset만큼 앞(벽에 막히면 벽 앞까지)의 바닥 위.
    public void CaptureDropKickTarget()
    {
        Vector2 playerPosition = Target.transform.position;
        float side = transform.position.x >= playerPosition.x ? 1f : -1f;
        Bounds bounds = BodyCollider.bounds;

        // 플레이어가 벽 가까이에 있으면 낙하 지점이 벽 안쪽으로 들어가지 않도록 벽 앞(몸 반폭)에서 자른다.
        float freeDistance = Movement.GetFreeDistanceToWall(playerPosition, side, _dropKickLandingOffset + bounds.extents.x) - bounds.extents.x;
        float landingX = playerPosition.x + side * Mathf.Clamp(freeDistance, 0f, _dropKickLandingOffset);

        // 낙하 지점 아래의 바닥에 발이 닿는 높이를 구한다. 바닥을 못 찾으면 플레이어 높이를 쓴다.
        if (!TryGetStandingHeight(landingX, out float landingY))
        {
            landingY = playerPosition.y;
        }

        DropKickTargetPosition = new Vector2(landingX, landingY);
    }

    // x 위치의 바닥 위에 섰을 때 Knight의 높이(물리 위치 기준)를 구한다. 아래에 바닥이 없으면 false를 반환한다.
    // Bounds는 물리 위치 기준이므로 발 높이도 물리 위치(Movement.Position)로 구한다.
    // transform.position은 Interpolate 때문에 뒤처져서, 오르내리는 중에 구하면 높이가 어긋난다.
    public bool TryGetStandingHeight(float x, out float height)
    {
        Vector2 bodyPosition = Movement.Position;
        RaycastHit2D groundHit = Physics2D.Raycast(new Vector2(x, bodyPosition.y), Vector2.down, GroundSearchDistance, _groundLayerMask);

        if (groundHit.collider == null)
        {
            height = bodyPosition.y;
            return false;
        }

        float feetOffset = bodyPosition.y - BodyCollider.bounds.min.y;
        height = groundHit.point.y + feetOffset;
        return true;
    }

    // 드롭킥이 낙하 지점에 부딪힐 때의 효과음을 재생한다.
    public void PlayDropKickImpactSound()
    {
        if (_dropKickImpactSound == null || !SoundManager.HasInstance)
        {
            return;
        }

        SoundManager.Instance.PlaySfx(_dropKickImpactSound);
    }

    // 드롭킥 충격 이펙트를 Knight 앞쪽에 띄운다. direction은 날아간 방향(오른쪽 1, 왼쪽 -1)이다.
    public void SpawnDropKickImpactEffect(float direction)
    {
        if (_dropKickImpactEffectPrefab == null || !PoolManager.HasInstance)
        {
            return;
        }

        Vector3 position = transform.position;
        position.x += _dropKickImpactEffectOffset.x * direction;
        position.y += _dropKickImpactEffectOffset.y;
        GameObject effect = PoolManager.Instance.Spawn(_dropKickImpactEffectPrefab, position, Quaternion.identity);

        // 프리팹 원본은 오른쪽 기준이므로 왼쪽으로 날아갔다면 좌우를 뒤집는다. (스케일은 PoolManager가 꺼낼 때 복원한다)
        if (effect != null && direction < 0f)
        {
            Vector3 scale = effect.transform.localScale;
            scale.x = -scale.x;
            effect.transform.localScale = scale;
        }
    }

    // 선택했을 때 공격 1회/2회의 휘두름 범위, 드롭킥 충격 범위, 슬라이딩 몸 충돌 범위를 그린다.
    protected override void DrawHitboxGizmos(float facingDirection)
    {
        DrawSwingGizmos(_attackSwings, "공격 1회", facingDirection);
        DrawSwingGizmos(_comboSwings, "공격 2회", facingDirection);
        DrawHitboxGizmo(_dropKickImpactHitbox, facingDirection, "드롭킥 충격");
        DrawHitboxGizmo(_slideBodyHitbox, facingDirection, "슬라이딩 몸 충돌");
    }

    private void DrawSwingGizmos(KnightSwing[] swings, string label, float facingDirection)
    {
        if (swings == null)
        {
            return;
        }

        for (int i = 0; i < swings.Length; i++)
        {
            DrawHitboxGizmo(swings[i].Hitbox, facingDirection, $"{label} - {i + 1}");
        }
    }

    // 패턴이 끝났을 때 기본 동작: Idle로 쉬어가며 플레이어에게 공격할 시간을 준다. Idle이 끝나면 다음 패턴을 고른다.
    public void ChangeToRest()
    {
        if (TryChangeToStun())
        {
            return;
        }

        ChangeToIdle(_idleDuration);
    }

    // 공격 1회/2회, 드롭킥 충격, 슬라이딩 몸 충돌이 플레이어를 맞혔을 때 호출한다. 맞힌 수만큼 게이지를 줄인다.
    public void ReducePatternGaugeByHits(int hitCount)
    {
        if (hitCount <= 0)
        {
            return;
        }

        PatternGauge.Reduce(_hitGaugeReduction * hitCount);
    }

    // 패턴이 끝나 다음 패턴(Idle, 구르기, 공격)으로 넘어가는 지점에서 호출한다.
    // 게이지가 꽉 찼으면 다음 패턴 대신 기절하고 true를 반환한다.
    private bool TryChangeToStun()
    {
        if (!PatternGauge.IsFull)
        {
            return false;
        }

        stateMachine.ChangeState(StunState);
        return true;
    }
}

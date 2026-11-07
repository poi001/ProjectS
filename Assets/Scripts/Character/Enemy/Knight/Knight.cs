using UnityEngine;

// 첫 번째 보스 Knight(임시 이름). GDD 6.6의 상태 구성과 전환 규칙을 따른다.
// 패턴 상태 인스턴스와 패턴 수치를 보관하고, 다음 패턴을 고르는 규칙(사거리 안: 공격 1회/2회, 사거리 밖: 점프/벽타기)을 담당한다.
// 패턴이 하나 끝나면 기본적으로 Idle로 쉬어간다. Idle 없이 이어지는 예외는 세 가지뿐이다.
// (공격 1회/2회 뒤 랜덤 구르기, 구르기 뒤 공격 1회/2회, 점프 뒤 공격 1회/2회) 구르기 직후의 공격 뒤에는 구르기를 하지 않는다.
// 각 패턴의 진행과 종료 판단은 Knight/States/의 상태가 담당한다.
// 수치는 기획에서 정해지지 않은 임시값이며 플레이하면서 인스펙터에서 조정한다. (GDD 9장)
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
    [Tooltip("벽에 매달리는 높이입니다. Knight가 벽의 이 높이를 향해 점프해서 매달립니다. (단위: 월드 Y, Knight 위치 기준)\n바닥과 너무 가까우면 매달리자마자 접지로 판정될 수 있으므로 바닥에서 충분히 높게 둡니다.")]
    [SerializeField] private float _wallHangHeight = 1.5f;
    [Tooltip("벽에 닿은 뒤 매달리는 높이와의 작은 오차를 벽을 타며 맞추는 속도입니다. 점프가 매달리는 지점을 향하므로 평소에는 거의 움직이지 않습니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _wallClimbSpeed = 15f;
    [Tooltip("벽에 매달려 있는 시간입니다. 이 시간이 지나면 플레이어 쪽으로 낙하(슬라이딩)합니다. 매달리는 순간의 플레이어 위치를 낙하 지점으로 정합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _wallHangDuration = 1f;

    [Header("슬라이딩 (대각선 낙하)")]
    [Tooltip("벽에서 낙하 지점 쪽으로 대각선으로 날아가는 속도입니다. 클수록 빠르게 떨어집니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _slideSpeed = 20f;
    [Tooltip("낙하 지점이 플레이어에게서 Knight 쪽으로 얼마나 앞에 있는지입니다. 플레이어가 아니라 그 앞쪽 바닥에 떨어집니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _slideLandingOffset = 1.5f;
    [Tooltip("낙하가 이 시간을 넘기면 바닥에 닿지 않았어도 그 자리에서 멈추고 충격을 냅니다. 예외 상황(어딘가에 걸린 경우) 대비용입니다. (단위: 초)")]
    [SerializeField, Min(0.1f)] private float _slideMaxDuration = 1.5f;

    [Header("슬라이딩 충격")]
    [Tooltip("낙하하다 바닥에 닿을 때 한 번 터지는 충격의 판정 범위입니다.\n오프셋 x는 날아간 방향을 기준으로 하며, 가로가 더 긴 직사각형으로 설정합니다.")]
    [SerializeField] private HitboxShape _slideImpactHitbox = new HitboxShape(new Vector2(2f, 0f), new Vector2(5f, 2.5f));
    [Tooltip("충격의 피해량 배율입니다. 실제 피해 = Knight 공격력 × 이 값입니다.\n1 = 공격력 그대로")]
    [SerializeField, Min(0f)] private float _slideImpactDamageMultiplier = 1f;
    [Tooltip("충격이 터질 때 소환할 이펙트 프리팹입니다. 풀(PoolManager)에서 꺼내며, 비워 두면 이펙트 없이 판정만 합니다.")]
    [SerializeField] private GameObject _slideImpactEffectPrefab;
    [Tooltip("충격 이펙트를 띄울 위치입니다. Knight 위치 기준 오프셋이며, 날아간 방향이 오른쪽일 때 기준입니다. 왼쪽이면 x를 뒤집습니다. (단위: 월드 거리)\n이펙트 그림은 아래쪽에 모여 있으므로 판정 범위의 중심보다 y를 조금 높게 둡니다. 값을 줄이면 이펙트가 바닥 쪽으로 내려갑니다.")]
    [SerializeField] private Vector2 _slideImpactEffectOffset = new Vector2(2f, 0.4f);
    [Tooltip("바닥에 부딪힐 때 재생할 효과음 데이터입니다. SoundManager의 Sfx 채널로 재생하며, 비워 두면 소리 없이 진행합니다.")]
    [SerializeField] private SoundDataSO _slideImpactSound;

    // 낙하 지점의 바닥 높이를 찾을 때 쓰는 Ground 레이어 마스크
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
    public KnightSlideState SlideState { get; private set; }

    public float AttackStepSpeed => _attackStepSpeed;
    public KnightSwing[] AttackSwings => _attackSwings;
    public KnightSwing[] ComboSwings => _comboSwings;
    public float RollSpeed => _rollSpeed;
    public float JumpMaxSpeedX => _jumpMaxSpeedX;
    public float WallHangHeight => _wallHangHeight;
    public float WallClimbSpeed => _wallClimbSpeed;
    public float WallHangDuration => _wallHangDuration;
    public float SlideSpeed => _slideSpeed;
    public float SlideMaxDuration => _slideMaxDuration;
    public HitboxShape SlideImpactHitbox => _slideImpactHitbox;
    public float SlideImpactDamage => Stats.AttackPower * _slideImpactDamageMultiplier;
    // 벽에 매달리는 순간 포착한 슬라이딩 낙하 지점(월드 좌표). WallClimb가 정하고 Slide가 쓴다.
    public Vector2 SlideTargetPosition { get; private set; }

    public bool IsTargetInRange => DistanceToTarget <= _attackRange;

    protected override bool OnInit(BattleContext context)
    {
        if (!base.OnInit(context))
        {
            return false;
        }

        _knightAnimation = GetComponentInChildren<KnightAnimation>();
        _groundLayerMask = LayerMask.GetMask("Ground");

        if (_knightAnimation == null)
        {
            Debug.LogError("Knight의 Visuals에 KnightAnimation이 없습니다.", this);
            return false;
        }

        if (_attackSwings.Length == 0 || _comboSwings.Length == 0)
        {
            Debug.LogError("Knight의 공격 1회/2회 휘두름 목록이 비어 있습니다. 비어 있으면 공격해도 판정이 나가지 않습니다.", this);
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
        SlideState = new KnightSlideState(this, stateMachine);
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
        _canRollAfterAttack = canRollAfterAttack;
        stateMachine.ChangeState(Random.value < _comboChance ? ComboState : AttackState);
    }

    // 공격 1회/2회가 끝났을 때: 구르기가 가능하면 일정 확률로 Idle 없이 바로 구르기를 시전하고, 아니면 Idle로 간다.
    public void ChangeToPatternAfterAttack()
    {
        if (_canRollAfterAttack && Random.value < _rollChance)
        {
            stateMachine.ChangeState(RollState);
            return;
        }

        ChangeToRest();
    }

    // 벽에 매달리는 순간 호출한다. 그 시점의 플레이어 위치를 포착해 슬라이딩 낙하 지점을 정한다.
    // 낙하 지점: 플레이어에게서 Knight 쪽으로 _slideLandingOffset만큼 앞(벽에 막히면 벽 앞까지)의 바닥 위.
    public void CaptureSlideTarget()
    {
        Vector2 playerPosition = Target.transform.position;
        float side = transform.position.x >= playerPosition.x ? 1f : -1f;
        Bounds bounds = BodyCollider.bounds;

        // 플레이어가 벽 가까이에 있으면 낙하 지점이 벽 안쪽으로 들어가지 않도록 벽 앞(몸 반폭)에서 자른다.
        float freeDistance = Movement.GetFreeDistanceToWall(playerPosition, side, _slideLandingOffset + bounds.extents.x) - bounds.extents.x;
        float landingX = playerPosition.x + side * Mathf.Clamp(freeDistance, 0f, _slideLandingOffset);

        // 낙하 지점 아래의 바닥에 발이 닿는 높이를 구한다. 바닥을 못 찾으면 플레이어 높이를 쓴다.
        // Bounds는 물리 위치 기준이므로 발 높이도 물리 위치(Movement.Position)로 구한다.
        // transform.position은 Interpolate 때문에 뒤처져서, 오르내리는 중에 매달리면 낙하 지점 높이가 어긋난다.
        float landingY = playerPosition.y;
        Vector2 bodyPosition = Movement.Position;
        RaycastHit2D groundHit = Physics2D.Raycast(new Vector2(landingX, bodyPosition.y), Vector2.down, 100f, _groundLayerMask);

        if (groundHit.collider != null)
        {
            float feetOffset = bodyPosition.y - bounds.min.y;
            landingY = groundHit.point.y + feetOffset;
        }

        SlideTargetPosition = new Vector2(landingX, landingY);
    }

    // 슬라이딩이 낙하 지점에 부딪힐 때의 효과음을 재생한다.
    public void PlaySlideImpactSound()
    {
        if (_slideImpactSound == null || !SoundManager.HasInstance)
        {
            return;
        }

        SoundManager.Instance.PlaySfx(_slideImpactSound);
    }

    // 슬라이딩 충격 이펙트를 Knight 앞쪽에 띄운다. direction은 날아간 방향(오른쪽 1, 왼쪽 -1)이다.
    public void SpawnSlideImpactEffect(float direction)
    {
        if (_slideImpactEffectPrefab == null || !PoolManager.HasInstance)
        {
            return;
        }

        Vector3 position = transform.position;
        position.x += _slideImpactEffectOffset.x * direction;
        position.y += _slideImpactEffectOffset.y;
        GameObject effect = PoolManager.Instance.Spawn(_slideImpactEffectPrefab, position, Quaternion.identity);

        // 프리팹 원본은 오른쪽 기준이므로 왼쪽으로 날아갔다면 좌우를 뒤집는다. (스케일은 PoolManager가 꺼낼 때 복원한다)
        if (effect != null && direction < 0f)
        {
            Vector3 scale = effect.transform.localScale;
            scale.x = -scale.x;
            effect.transform.localScale = scale;
        }
    }

    // 선택했을 때 공격 1회/2회의 휘두름 범위와 슬라이딩 충격 범위를 그린다.
    protected override void DrawHitboxGizmos(float facingDirection)
    {
        DrawSwingGizmos(_attackSwings, "공격 1회", facingDirection);
        DrawSwingGizmos(_comboSwings, "공격 2회", facingDirection);
        DrawHitboxGizmo(_slideImpactHitbox, facingDirection, "슬라이딩 충격");
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
        ChangeToIdle(_idleDuration);
    }
}

using UnityEngine;

// 플레이어 컴포넌트 참조와 초기화 순서, 상태 인스턴스를 관리하고 상태기계를 구동한다.
// 상태 전환 판단은 각 상태가, 물리 처리는 CharacterMovement가, 입력과 입력 버퍼는 PlayerInputHandler가 담당한다.
[RequireComponent(typeof(PlayerInputHandler), typeof(CharacterVital))]
public class Player : Character
{
    // 공격자와 x 차이가 이보다 작으면 좌우를 구분할 수 없는 것으로 본다.
    private const float KnockbackDirectionThreshold = 0.01f;

    [Header("스탯")]
    [Tooltip("플레이어의 능력치 데이터(ScriptableObject)입니다. 이동 속도, 체력, 공격력 등이 들어 있습니다.")]
    [SerializeField] private PlayerStatsSO _stats;

    [Header("점프")]
    [Tooltip("발판에서 떨어진 뒤에도 이 시간 안에는 점프할 수 있습니다. (코요테 타임, 단위: 초)")]
    [SerializeField] private float _coyoteTime = 0.1f;
    [Tooltip("공중 점프의 높이 배율입니다. 1이면 땅에서 하는 점프와 같은 높이까지 오릅니다. 공중 점프 횟수는 PlayerStatsSO에서 정합니다.")]
    [SerializeField, Min(0f)] private float _airJumpHeightRatio = 1f;

    [Header("벽타기")]
    [Tooltip("벽을 타는 동안 미끄러져 내려가는 속도입니다. 0이면 벽에 붙은 채 멈춥니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _wallSlideSpeed = 2f;
    [Tooltip("벽을 타는 동안 그림을 벽 쪽으로 옮기는 거리입니다. 벽타기 스프라이트는 그림이 칸 가운데에서 벗어나 있어 그대로 두면 벽에서 떠 보입니다. 늘리면 벽에 더 붙고, 0이면 옮기지 않습니다. (단위: 월드 거리)")]
    [SerializeField] private float _wallSlideSpriteOffset = 0.3f;
    [Tooltip("벽 점프 시 벽 반대쪽으로 튀어 나가는 가로 속도입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _wallJumpSpeedX = 7f;
    [Tooltip("벽 점프의 높이 배율입니다. 1이면 땅에서 하는 점프와 같은 높이까지 오릅니다.")]
    [SerializeField, Min(0f)] private float _wallJumpHeightRatio = 1f;
    [Tooltip("벽 점프 직후 좌우 입력을 무시하는 시간입니다. 튀어 나가자마자 같은 벽으로 되돌아가지 않게 합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _wallJumpInputLockTime = 0.15f;

    [Header("대시")]
    [Tooltip("대시하는 동안의 가로 속도입니다. 이동 거리는 속도 × 지속 시간입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _dashSpeed = 15f;
    [Tooltip("대시 지속 시간입니다. 이 시간 동안 피격 판정이 꺼져 적의 공격에 걸리지 않습니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _dashDuration = 0.25f;
    [Tooltip("대시가 끝난 뒤 다시 대시할 수 있을 때까지의 시간입니다. 공중에서는 이와 별개로 착지하거나 벽을 탈 때까지 1번만 대시할 수 있습니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _dashCooldown = 0.5f;

    [Header("공격")]
    [Tooltip("지상 콤보의 단계별 공격 설정입니다. 배열 크기가 곧 콤보 수이며, Animator에 Combo{n}, Combo{n}_End 상태가 이 수만큼 있어야 합니다.\n단계마다 피해 배율과 판정 범위를 정합니다.")]
    [SerializeField] private PlayerAttack[] _comboAttacks =
    {
        new PlayerAttack(1f, new HitboxShape(new Vector2(1f, 0f), new Vector2(1.5f, 1f))),
        new PlayerAttack(1f, new HitboxShape(new Vector2(1f, 0f), new Vector2(1.5f, 1f))),
        new PlayerAttack(1f, new HitboxShape(new Vector2(1f, 0f), new Vector2(1.5f, 1f)))
    };
    [Tooltip("점프 공격의 판정 범위입니다. 피해량은 지상 콤보 1단계의 배율을 그대로 씁니다.")]
    [SerializeField] private HitboxShape _jumpAttackHitbox = new HitboxShape(new Vector2(1f, 0f), new Vector2(1.5f, 1f));

    [Header("피격")]
    [Tooltip("피해를 입은 순간부터 무적인 시간입니다. 넉백 시간도 포함됩니다. (단위: 초)")]
    [SerializeField] private float _invincibleDuration = 1f;
    [Tooltip("피격 시 넉백 속도입니다. x는 피해를 입은 방향의 반대로 밀리는 속도, y는 위로 뜨는 속도입니다. (단위: 월드 거리/초)")]
    [SerializeField] private Vector2 _knockbackVelocity = new Vector2(5f, 5f);

    private StateMachine<Player> _stateMachine;
    // 부모의 Animation 자동 프로퍼티와 직렬화 필드 이름이 겹치지 않도록 별도 필드에 보관한다.
    private PlayerAnimation _playerAnimation;
    // 공중 행동 자원. 땅에 닿거나 벽을 타면 다시 채워진다. (RefillAirActions)
    private int _remainingAirJumps;
    private bool _hasAirDash;
    // 다시 대시할 수 있게 되는 시각 (대시 쿨타임)
    private float _dashReadyTime = float.NegativeInfinity;

    public PlayerInputHandler InputHandler { get; private set; }
    public CharacterVital Vital { get; private set; }
    public new PlayerAnimation Animation => _playerAnimation;
    public PlayerStatsSO Stats => _stats;
    public HitboxShape JumpAttackHitbox => _jumpAttackHitbox;
    // 점프 공격의 데미지 (공격력 × 지상 콤보 1단계의 배율)
    public float JumpAttackDamage => _comboAttacks.Length > 0 ? GetDamage(_comboAttacks[0]) : _stats.AttackPower;
    public Vector2 KnockbackVelocity => _knockbackVelocity;
    public float AirJumpHeightRatio => _airJumpHeightRatio;
    public float WallSlideSpeed => _wallSlideSpeed;
    public float WallSlideSpriteOffset => _wallSlideSpriteOffset;
    public float WallJumpSpeedX => _wallJumpSpeedX;
    public float WallJumpHeightRatio => _wallJumpHeightRatio;
    public float WallJumpInputLockTime => _wallJumpInputLockTime;
    public float DashSpeed => _dashSpeed;
    public float DashDuration => _dashDuration;

    public PlayerIdleState IdleState { get; private set; }
    public PlayerRunState RunState { get; private set; }
    public PlayerJumpState JumpState { get; private set; }
    public PlayerAirJumpState AirJumpState { get; private set; }
    public PlayerFallState FallState { get; private set; }
    public PlayerWallSlideState WallSlideState { get; private set; }
    public PlayerWallJumpState WallJumpState { get; private set; }
    public PlayerDashState DashState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }
    public PlayerJumpAttackState JumpAttackState { get; private set; }
    public PlayerHitState HitState { get; private set; }
    public PlayerDeadState DeadState { get; private set; }

    // Animator에서 실제로 사용할 수 있는 지상 콤보 수
    public int ComboCount { get; private set; }

    public State<Player> CurrentState => _stateMachine.CurrentState;

    protected override string HurtboxLayerName => "Player";
    protected override string TargetLayerName => "Enemy";

    // 점프 버퍼 시간 안에 입력이 있었고, 코요테 타임 안에 접지했었다면 점프 가능
    public bool CanJump => InputHandler.HasBufferedJump && Movement.TimeSinceGrounded <= _coyoteTime;

    // 공중에서 점프 입력이 있고 공중 점프 횟수가 남아 있다면 공중 점프 가능 (코요테 타임 안이라면 CanJump가 먼저 쓰인다)
    public bool CanAirJump => InputHandler.HasBufferedJump && !Movement.IsGrounded && _remainingAirJumps > 0;

    // 대시 입력이 있고, 쿨타임이 지났으며, 땅 위이거나 공중 대시가 남아 있다면 대시 가능
    public bool CanDash => InputHandler.HasBufferedDash && Time.time >= _dashReadyTime && (Movement.IsGrounded || _hasAirDash);

    // 초기화 순서: 컴포넌트 참조 → 스탯 적용 → 상태 생성 → Animator 설정. 이벤트 구독과 상태기계 시작은 OnInitCompleted에서 한다.
    protected override bool OnInit(BattleContext context)
    {
        if (!base.OnInit(context))
        {
            return false;
        }

        if (_stats == null)
        {
            Debug.LogError("Player에 PlayerStatsSO가 지정되지 않았습니다.", this);
            return false;
        }

        InputHandler = GetComponent<PlayerInputHandler>();
        Vital = GetComponent<CharacterVital>();
        _playerAnimation = GetComponentInChildren<PlayerAnimation>();

        if (_playerAnimation == null)
        {
            Debug.LogError("Player의 Visuals에 PlayerAnimation이 없습니다.", this);
            return false;
        }

        ApplyStats();
        CreateStates();

        ComboCount = Animation.SetupCombos(Mathf.Max(1, _comboAttacks.Length));
        Animation.SetAttackSpeed(_stats.AttackSpeed);
        ValidateAttackSettings();
        ValidateAnimationStates();
        return true;
    }

    protected override void OnInitCompleted()
    {
        base.OnInitCompleted();
        Vital.Damaged += OnDamaged;
        Vital.Died += OnDied;
        _stateMachine.Initialize(IdleState);
    }

    protected override void OnRelease()
    {
        Vital.Damaged -= OnDamaged;
        Vital.Died -= OnDied;
        base.OnRelease();
    }

    private void Update()
    {
        if (!IsInitialized)
        {
            return;
        }

        // 땅에 닿아 있는 동안 공중 점프와 공중 대시를 다시 채운다.
        if (Movement.IsGrounded)
        {
            RefillAirActions();
        }

        _stateMachine.LogicUpdate();
    }

    private void FixedUpdate()
    {
        if (!IsInitialized)
        {
            return;
        }

        _stateMachine.PhysicsUpdate();
    }

    // 공중 점프 횟수와 공중 대시를 다시 채운다. 땅에 닿아 있을 때와 벽을 타기 시작할 때 호출된다.
    public void RefillAirActions()
    {
        _remainingAirJumps = _stats.AirJumpCount;
        _hasAirDash = true;
    }

    public void ConsumeAirJump()
    {
        _remainingAirJumps = Mathf.Max(0, _remainingAirJumps - 1);
    }

    // 대시를 시작할 때 호출한다. 공중에서 시작했다면 공중 대시를 소모한다.
    public void ConsumeDash()
    {
        if (!Movement.IsGrounded)
        {
            _hasAirDash = false;
        }
    }

    // 대시가 끝날 때 호출한다. 지금부터 쿨타임이 지나야 다시 대시할 수 있다.
    public void StartDashCooldown()
    {
        _dashReadyTime = Time.time + _dashCooldown;
    }

    // 해당 콤보 단계의 공격 설정. 설정되지 않았다면 null을 반환한다.
    public PlayerAttack GetComboAttack(int comboIndex)
    {
        if (comboIndex >= _comboAttacks.Length)
        {
            return null;
        }

        return _comboAttacks[comboIndex];
    }

    // 공격의 데미지 (공격력 × 공격 배율)
    public float GetDamage(PlayerAttack attack)
    {
        return _stats.AttackPower * attack.DamageMultiplier;
    }

    // 선택했을 때 지상 콤보 단계별 범위와 점프 공격 범위를 그린다.
    protected override void DrawHitboxGizmos(float facingDirection)
    {
        if (_comboAttacks != null)
        {
            for (int i = 0; i < _comboAttacks.Length; i++)
            {
                DrawHitboxGizmo(_comboAttacks[i].Hitbox, facingDirection, $"콤보 {i + 1}");
            }
        }

        DrawHitboxGizmo(_jumpAttackHitbox, facingDirection, "점프 공격");
    }

    private void ApplyStats()
    {
        Movement.SetMoveSpeed(_stats.MoveSpeed);
        Vital.Initialize(_stats.MaxHealth);
    }

    // 콤보 공격 설정이 비어 있으면 지상 공격이 판정을 내지 못하므로 알린다.
    private void ValidateAttackSettings()
    {
        if (_comboAttacks.Length == 0)
        {
            Debug.LogError("지상 콤보 공격 설정이 비어 있습니다. 공격해도 판정이 나가지 않습니다.", this);
        }
    }

    // 애니메이션이 끝나야 빠져나가는 상태(Hit, WallJump)는 Animator에 상태가 없으면 끝나지 않으므로 미리 알린다.
    // 나머지 새 동작(WallSlide, Dash)은 상태가 없어도 멈추지는 않지만 모습이 바뀌지 않으므로 함께 확인한다.
    private void ValidateAnimationStates()
    {
        ValidateAnimationState(PlayerAnimation.Hit, "Hit");
        ValidateAnimationState(PlayerAnimation.WallJump, "WallJump");
        ValidateAnimationState(PlayerAnimation.WallSlide, "WallSlide");
        ValidateAnimationState(PlayerAnimation.Dash, "Dash");
    }

    private void ValidateAnimationState(int stateHash, string stateName)
    {
        if (!Animation.HasState(stateHash))
        {
            Debug.LogError($"Animator에 '{stateName}' 상태가 없습니다.", this);
        }
    }

    private void CreateStates()
    {
        _stateMachine = new StateMachine<Player>();
        IdleState = new PlayerIdleState(this, _stateMachine);
        RunState = new PlayerRunState(this, _stateMachine);
        JumpState = new PlayerJumpState(this, _stateMachine);
        AirJumpState = new PlayerAirJumpState(this, _stateMachine);
        FallState = new PlayerFallState(this, _stateMachine);
        WallSlideState = new PlayerWallSlideState(this, _stateMachine);
        WallJumpState = new PlayerWallJumpState(this, _stateMachine);
        DashState = new PlayerDashState(this, _stateMachine);
        AttackState = new PlayerAttackState(this, _stateMachine);
        JumpAttackState = new PlayerJumpAttackState(this, _stateMachine);
        HitState = new PlayerHitState(this, _stateMachine);
        DeadState = new PlayerDeadState(this, _stateMachine);
    }

    // 피해를 입으면 무적을 시작하고 진행 중이던 동작을 끊고 피격 상태로 전환한다.
    private void OnDamaged(DamageInfo damageInfo)
    {
        // 치명 피해는 Died에서 사망 상태로 전환하므로 피격 상태를 거치지 않는다.
        if (Vital.IsDead)
        {
            return;
        }

        Vital.SetInvincible(_invincibleDuration);
        HitState.SetKnockbackDirection(GetKnockbackDirection(damageInfo));
        _stateMachine.ChangeState(HitState);
    }

    // 피해를 준 쪽의 반대 방향 (오른쪽 1, 왼쪽 -1). 공격자 위치를 기준으로 하고, 공격자 정보가 없으면 피격 지점을 기준으로 한다.
    // 기준 위치와 x가 같아 방향을 알 수 없으면 바라보는 방향의 반대로 한다.
    private float GetKnockbackDirection(DamageInfo damageInfo)
    {
        float sourceX = damageInfo.Instigator != null ? damageInfo.Instigator.transform.position.x : damageInfo.HitPoint.x;
        float deltaX = transform.position.x - sourceX;

        if (Mathf.Abs(deltaX) > KnockbackDirectionThreshold)
        {
            return Mathf.Sign(deltaX);
        }

        return -Animation.FacingDirection;
    }

    private void OnDied()
    {
        _stateMachine.ChangeState(DeadState);
    }
}

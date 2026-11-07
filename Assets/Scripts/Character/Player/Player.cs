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

    public PlayerInputHandler InputHandler { get; private set; }
    public CharacterVital Vital { get; private set; }
    public new PlayerAnimation Animation => _playerAnimation;
    public PlayerStatsSO Stats => _stats;
    public HitboxShape JumpAttackHitbox => _jumpAttackHitbox;
    // 점프 공격의 데미지 (공격력 × 지상 콤보 1단계의 배율)
    public float JumpAttackDamage => _comboAttacks.Length > 0 ? GetDamage(_comboAttacks[0]) : _stats.AttackPower;
    public Vector2 KnockbackVelocity => _knockbackVelocity;

    public PlayerIdleState IdleState { get; private set; }
    public PlayerRunState RunState { get; private set; }
    public PlayerJumpState JumpState { get; private set; }
    public PlayerFallState FallState { get; private set; }
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
        ValidateHitSettings();
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

    // Hit 애니메이션 상태가 없으면 피격 상태가 끝나지 않으므로 미리 알린다.
    private void ValidateHitSettings()
    {
        if (!Animation.HasState(PlayerAnimation.Hit))
        {
            Debug.LogError("Animator에 'Hit' 상태가 없어 피격 상태가 끝나지 않습니다.", this);
        }
    }

    private void CreateStates()
    {
        _stateMachine = new StateMachine<Player>();
        IdleState = new PlayerIdleState(this, _stateMachine);
        RunState = new PlayerRunState(this, _stateMachine);
        JumpState = new PlayerJumpState(this, _stateMachine);
        FallState = new PlayerFallState(this, _stateMachine);
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

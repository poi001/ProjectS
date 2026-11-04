using UnityEngine;

// 플레이어 컴포넌트 참조와 초기화 순서, 상태 인스턴스를 관리하고 상태기계를 구동한다.
// 상태 전환 판단은 각 상태가, 물리 처리는 CharacterMovement가, 입력과 입력 버퍼는 PlayerInputHandler가 담당한다.
[RequireComponent(typeof(PlayerInputHandler), typeof(CharacterVital))]
public class Player : Character
{
    [Header("스탯")]
    [SerializeField] private PlayerStatsSO _stats;

    [Header("점프")]
    [SerializeField] private float _coyoteTime = 0.1f;

    [Header("공격")]
    // 캐릭터가 가진 지상 콤보 수. Animator에 Combo{n}, Combo{n}_End 상태가 이 수만큼 있어야 한다.
    [SerializeField] private int _comboCount = 3;
    // 콤보별 데미지 계수. 데미지 = 공격력 × 계수. 점프 공격은 콤보 1의 계수를 쓴다.
    [SerializeField] private float[] _comboDamageMultipliers = { 1f, 1f, 1f };
    // 콤보별 공격 판정. 범위가 같다면 같은 판정을 여러 칸에 넣어도 된다.
    [SerializeField] private AttackHitbox[] _comboHitboxes = new AttackHitbox[0];
    [SerializeField] private AttackHitbox _jumpAttackHitbox;

    private StateMachine<Player> _stateMachine;
    // 부모의 Animation 자동 프로퍼티와 직렬화 필드 이름이 겹치지 않도록 별도 필드에 보관한다.
    private PlayerAnimation _playerAnimation;

    public PlayerInputHandler InputHandler { get; private set; }
    public CharacterVital Vital { get; private set; }
    public new PlayerAnimation Animation => _playerAnimation;
    public PlayerStatsSO Stats => _stats;
    public AttackHitbox JumpAttackHitbox => _jumpAttackHitbox;

    public PlayerIdleState IdleState { get; private set; }
    public PlayerRunState RunState { get; private set; }
    public PlayerJumpState JumpState { get; private set; }
    public PlayerFallState FallState { get; private set; }
    public PlayerAttackState AttackState { get; private set; }
    public PlayerJumpAttackState JumpAttackState { get; private set; }
    public PlayerDeadState DeadState { get; private set; }

    // Animator에서 실제로 사용할 수 있는 지상 콤보 수
    public int ComboCount { get; private set; }

    public State<Player> CurrentState => _stateMachine.CurrentState;

    protected override string HurtboxLayerName => "Player";

    // 점프 버퍼 시간 안에 입력이 있었고, 코요테 타임 안에 접지했었다면 점프 가능
    public bool CanJump => InputHandler.HasBufferedJump && Movement.TimeSinceGrounded <= _coyoteTime;

    // 초기화 순서: 컴포넌트 참조 → 스탯 적용 → 상태 생성. Animator 관련 초기화는 Start에서 한다.
    protected override void Awake()
    {
        base.Awake();

        if (_stats == null)
        {
            Debug.LogError("Player에 PlayerStatsSO가 지정되지 않았습니다.", this);
            enabled = false;
            return;
        }

        InputHandler = GetComponent<PlayerInputHandler>();
        Vital = GetComponent<CharacterVital>();
        _playerAnimation = GetComponentInChildren<PlayerAnimation>();

        ApplyStats();
        CreateStates();
    }

    private void OnEnable()
    {
        Vital.Died += OnDied;
    }

    private void OnDisable()
    {
        Vital.Died -= OnDied;
    }

    // Animator는 자식(Visuals) 오브젝트에 있어 Awake 시점에는 아직 활성화되지 않았을 수 있으므로 Start에서 초기화한다.
    private void Start()
    {
        ComboCount = Animation.SetupCombos(Mathf.Max(1, _comboCount));
        Animation.SetAttackSpeed(_stats.AttackSpeed);
        ValidateAttackSettings();
        _stateMachine.Initialize(IdleState);
    }

    private void Update()
    {
        _stateMachine.LogicUpdate();
    }

    private void FixedUpdate()
    {
        _stateMachine.PhysicsUpdate();
    }

    // 해당 콤보의 데미지 (공격력 × 콤보 계수). 계수가 부족하면 마지막 계수를 쓴다.
    public float GetComboDamage(int comboIndex)
    {
        if (_comboDamageMultipliers.Length == 0)
        {
            return _stats.AttackPower;
        }

        int multiplierIndex = Mathf.Min(comboIndex, _comboDamageMultipliers.Length - 1);
        return _stats.AttackPower * _comboDamageMultipliers[multiplierIndex];
    }

    // 해당 콤보의 공격 판정. 설정되지 않았다면 null을 반환한다.
    public AttackHitbox GetComboHitbox(int comboIndex)
    {
        if (comboIndex >= _comboHitboxes.Length)
        {
            return null;
        }

        return _comboHitboxes[comboIndex];
    }

    private void ApplyStats()
    {
        Movement.SetMoveSpeed(_stats.MoveSpeed);
        Vital.Initialize(_stats.MaxHealth);
    }

    // 콤보 수에 비해 계수나 판정 설정이 부족하면 알린다.
    private void ValidateAttackSettings()
    {
        if (_comboDamageMultipliers.Length < ComboCount)
        {
            Debug.LogWarning($"콤보 데미지 계수가 콤보 수({ComboCount})보다 적어, 부족한 콤보는 마지막 계수를 사용합니다.", this);
        }

        for (int i = 0; i < ComboCount; i++)
        {
            if (GetComboHitbox(i) == null)
            {
                Debug.LogError($"콤보 {i + 1}의 공격 판정(AttackHitbox)이 지정되지 않았습니다.", this);
            }
        }

        if (_jumpAttackHitbox == null)
        {
            Debug.LogError("점프 공격의 공격 판정(AttackHitbox)이 지정되지 않았습니다.", this);
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
        DeadState = new PlayerDeadState(this, _stateMachine);
    }

    private void OnDied()
    {
        _stateMachine.ChangeState(DeadState);
    }
}

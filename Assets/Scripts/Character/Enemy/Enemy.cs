using UnityEngine;

// 적(보스) 공통 베이스. 컴포넌트 참조와 초기화 순서, 공통 상태(Idle/Dead), 타깃(플레이어) 참조를 관리하고 상태기계를 구동한다.
// 보스마다 패턴이 다르므로 각 보스는 이 클래스를 상속(예: Knight)하고,
// CreateStates를 오버라이드해 고유 패턴 상태를 추가하고 ChangeToNextPattern에서 다음 패턴을 고른다.
[RequireComponent(typeof(CharacterVital))]
public abstract class Enemy : Character
{
    [Header("스탯")]
    [SerializeField] private EnemyStatsSO _stats;

    [Header("시작")]
    // 전투 시작 후 첫 패턴을 시전하기 전까지 Idle로 기다리는 시간
    [SerializeField, Min(0f)] private float _startIdleDuration = 1f;

    protected StateMachine<Enemy> stateMachine;
    // 부모의 Animation 자동 프로퍼티와 직렬화 필드 이름이 겹치지 않도록 별도 필드에 보관한다.
    private EnemyAnimation _enemyAnimation;

    public CharacterVital Vital { get; private set; }
    public new EnemyAnimation Animation => _enemyAnimation;
    public EnemyStatsSO Stats => _stats;
    public Player Target { get; private set; }

    public EnemyIdleState IdleState { get; private set; }
    public EnemyDeadState DeadState { get; private set; }

    public State<Enemy> CurrentState => stateMachine.CurrentState;

    // 타깃이 없거나 죽었다면 패턴을 시전하지 않고 Idle을 유지한다. (GDD 6.6)
    public bool IsTargetDead => Target == null || Target.Vital.IsDead;
    // 타깃이 있는 가로 방향 (오른쪽 1, 왼쪽 -1)
    public float DirectionToTarget => Target.transform.position.x >= transform.position.x ? 1f : -1f;
    public float DistanceToTarget => Mathf.Abs(Target.transform.position.x - transform.position.x);

    protected override string HurtboxLayerName => "Enemy";

    // 초기화 순서: 컴포넌트 참조 → 스탯 적용 → 상태 생성. 타깃 탐색과 상태기계 시작은 Start에서 한다.
    protected override void Awake()
    {
        base.Awake();

        if (_stats == null)
        {
            Debug.LogError("Enemy에 EnemyStatsSO가 지정되지 않았습니다.", this);
            enabled = false;
            return;
        }

        Vital = GetComponent<CharacterVital>();
        _enemyAnimation = GetComponentInChildren<EnemyAnimation>();

        ApplyStats();
        CreateStates();
    }

    private void OnEnable()
    {
        Vital.Damaged += OnDamaged;
        Vital.Died += OnDied;
    }

    private void OnDisable()
    {
        Vital.Damaged -= OnDamaged;
        Vital.Died -= OnDied;
    }

    // 플레이어는 Awake에서 Vital을 준비하므로, 모든 Awake가 끝난 Start에서 타깃을 찾는다.
    // Animator는 자식(Visuals) 오브젝트에 있어 Awake 시점에는 아직 활성화되지 않았을 수 있으므로 Start에서 초기화한다.
    private void Start()
    {
        Target = FindAnyObjectByType<Player>();

        if (Target == null)
        {
            Debug.LogWarning($"{name}: 씬에 Player가 없어 패턴을 시전하지 않습니다.", this);
        }
        else
        {
            Target.Vital.Died += OnTargetDied;
        }

        Animation.SetAttackSpeed(_stats.AttackSpeed);
        IdleState.SetDuration(_startIdleDuration);
        stateMachine.Initialize(IdleState);
    }

    private void OnDestroy()
    {
        if (Target != null)
        {
            Target.Vital.Died -= OnTargetDied;
        }
    }

    private void Update()
    {
        stateMachine.LogicUpdate();
    }

    private void FixedUpdate()
    {
        stateMachine.PhysicsUpdate();
    }

    public void FaceTarget()
    {
        Animation.UpdateFacing(DirectionToTarget);
    }

    // 정해진 시간 동안 Idle로 쉬어간 뒤 다음 패턴을 고른다.
    public void ChangeToIdle(float duration)
    {
        IdleState.SetDuration(duration);
        stateMachine.ChangeState(IdleState);
    }

    // Idle이 끝났을 때 호출된다. 보스마다 다음 패턴을 고르는 규칙이 다르므로 자식 클래스가 구현한다.
    public abstract void ChangeToNextPattern();

    private void ApplyStats()
    {
        Vital.Initialize(_stats.MaxHealth);
    }

    // 공통 상태를 생성한다. 자식 클래스에서 오버라이드할 때는 반드시 base.CreateStates()를 먼저 호출한다.
    protected virtual void CreateStates()
    {
        stateMachine = new StateMachine<Enemy>();
        IdleState = new EnemyIdleState(this, stateMachine);
        DeadState = new EnemyDeadState(this, stateMachine);
    }

    // 임시 디버그용: 피해 판정 확인을 위한 로그. 확인이 끝나면 제거한다.
    private void OnDamaged(DamageInfo damageInfo)
    {
        string instigatorName = damageInfo.Instigator != null ? damageInfo.Instigator.name : "없음";
        Debug.Log($"{name} 피격: -{damageInfo.Amount} (체력 {Vital.CurrentHealth}/{Vital.MaxHealth}, 공격자 {instigatorName})", this);
    }

    private void OnDied()
    {
        stateMachine.ChangeState(DeadState);
    }

    // 플레이어가 죽으면 진행 중인 패턴을 멈추고 Idle을 유지한다. (IdleState는 타깃이 죽어 있는 동안 다음 패턴으로 넘어가지 않는다.)
    private void OnTargetDied()
    {
        if (Vital.IsDead)
        {
            return;
        }

        ChangeToIdle(0f);
    }
}

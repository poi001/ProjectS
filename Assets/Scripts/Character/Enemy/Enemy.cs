using UnityEngine;

// 적(보스) 공통 베이스. 컴포넌트 참조와 초기화 순서, 공통 상태(Idle/Dead), 타깃(플레이어) 참조를 관리하고 상태기계를 구동한다.
// 보스마다 패턴이 다르므로 각 보스는 이 클래스를 상속(예: Knight)하고,
// CreateStates를 오버라이드해 고유 패턴 상태를 추가하고 ChangeToNextPattern에서 다음 패턴을 고른다.
// 페이즈: 다음 페이즈가 남은 채로 체력을 모두 잃으면 죽지 않고 페이즈 전환을 한다. (모든 보스 공통)
//  - 체력 소진 → OnPhaseHealthDepleted(보스별 전환 연출) → StartPhaseRefill(체력 충전, 공통) → 다음 페이즈 시작(OnPhaseStarted) → Idle
//  - 전환이 끝날 때까지 피해를 입지 않는다. 마지막 페이즈에서 체력을 모두 잃으면 기존처럼 사망한다.
[RequireComponent(typeof(CharacterVital))]
public abstract class Enemy : Character
{
    [Header("스탯")]
    [Tooltip("이 적의 능력치 데이터(ScriptableObject)입니다. 체력, 공격력 등이 들어 있습니다.")]
    [SerializeField] private EnemyStatsSO _stats;

    [Header("종류")]
    [Tooltip("보스인지 여부입니다. 켜면 죽을 때 KO 연출(슬로우 모션, 줌)과 보스 KO 사운드가 나옵니다. 보스가 소환하는 잡몹은 끕니다.")]
    [SerializeField] private bool _isBoss;

    [Header("시작")]
    [Tooltip("전투 시작 후 첫 패턴을 시전하기 전까지 Idle로 기다리는 시간입니다. 페이즈 전환이 끝나 새 페이즈를 시작할 때도 같은 시간만큼 기다립니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _startIdleDuration = 1f;

    [Header("페이즈")]
    [Tooltip("2페이즈부터 각 페이즈의 최대 체력입니다. 항목 하나가 페이즈 하나입니다. (비워 두면 1페이즈만 있는 보스, 1개면 2페이즈 보스)\n0으로 두면 1페이즈의 최대 체력(스탯)을 그대로 씁니다.")]
    [SerializeField, Min(0f)] private float[] _nextPhaseMaxHealths = new float[0];

    [Tooltip("페이즈 전환 때 체력이 0에서 최대까지 차오르는 데 걸리는 시간입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _phaseRefillDuration = 1.5f;

    protected StateMachine<Enemy> stateMachine;
    // 부모의 Animation 자동 프로퍼티와 직렬화 필드 이름이 겹치지 않도록 별도 필드에 보관한다.
    private EnemyAnimation _enemyAnimation;

    public CharacterVital Vital { get; private set; }
    // 패턴 게이지. 게이지를 쓰는 보스만 프리팹 루트에 PatternGauge를 붙이며, 없으면 null이다. 사용 방법은 보스마다 다르다.
    public PatternGauge PatternGauge { get; private set; }
    // 머리 위 상태 VFX(기절, 감지, 버프 등) 표시. 프리팹 루트에 StatusVfxDisplay가 없으면 null이며, 그러면 상태 VFX를 띄우지 않는다.
    public StatusVfxDisplay StatusVfx { get; private set; }
    public new EnemyAnimation Animation => _enemyAnimation;
    public EnemyStatsSO Stats => _stats;
    public bool IsBoss => _isBoss;
    public Player Target { get; private set; }

    public EnemyIdleState IdleState { get; private set; }
    public EnemyDeadState DeadState { get; private set; }
    public EnemyPhaseRefillState PhaseRefillState { get; private set; }

    public State<Enemy> CurrentState => stateMachine.CurrentState;

    // 현재 페이즈. 전투가 시작되면 1이다.
    public int CurrentPhase { get; private set; }
    public int PhaseCount => (_nextPhaseMaxHealths != null ? _nextPhaseMaxHealths.Length : 0) + 1;
    public bool HasNextPhase => CurrentPhase < PhaseCount;
    // 페이즈 전환 중인지 여부. 체력을 모두 잃은 순간부터 다음 페이즈의 체력이 다 찰 때까지 true이며, 이 동안 피해를 입지 않는다.
    public bool IsPhaseTransitioning => Vital != null && Vital.IsDepleted;
    public float PhaseRefillDuration => _phaseRefillDuration;

    // 다음 페이즈의 최대 체력. 따로 정하지 않았거나(0) 다음 페이즈가 없으면 1페이즈의 최대 체력(스탯)이다.
    public float NextPhaseMaxHealth
    {
        get
        {
            float maxHealth = HasNextPhase ? _nextPhaseMaxHealths[CurrentPhase - 1] : 0f;
            return maxHealth > 0f ? maxHealth : _stats.MaxHealth;
        }
    }

    // 타깃이 없거나 죽었다면 패턴을 시전하지 않고 Idle을 유지한다. (GDD 6.6)
    public bool IsTargetDead => Target == null || Target.Vital.IsDead;
    // 타깃이 있는 가로 방향 (오른쪽 1, 왼쪽 -1)
    public float DirectionToTarget => Target.transform.position.x >= transform.position.x ? 1f : -1f;
    public float DistanceToTarget => Mathf.Abs(Target.transform.position.x - transform.position.x);

    protected override string HurtboxLayerName => "Enemy";
    protected override string TargetLayerName => "Player";

    // 초기화 순서: 컴포넌트 참조 → 스탯 적용 → 상태 생성 → 타깃 지정. 이벤트 구독과 상태기계 시작은 OnInitCompleted에서 한다.
    // 타깃은 전투 정보(BattleContext)의 플레이어다. 플레이어가 먼저 Init되어 있어야 한다.
    protected override bool OnInit(BattleContext context)
    {
        if (!base.OnInit(context))
        {
            return false;
        }

        if (_stats == null)
        {
            Debug.LogError("Enemy에 EnemyStatsSO가 지정되지 않았습니다.", this);
            return false;
        }

        Vital = GetComponent<CharacterVital>();
        _enemyAnimation = GetComponentInChildren<EnemyAnimation>();

        // 게이지가 없는 보스는 null로 둔다. (TryGetComponent는 없을 때 진짜 null을 준다.)
        PatternGauge = TryGetComponent(out PatternGauge patternGauge) ? patternGauge : null;
        StatusVfx = TryGetComponent(out StatusVfxDisplay statusVfx) ? statusVfx : null;

        if (_enemyAnimation == null)
        {
            Debug.LogError($"{name}: Visuals에 EnemyAnimation이 없습니다.", this);
            return false;
        }

        ApplyStats();
        CreateStates();
        Animation.SetAttackSpeed(_stats.AttackSpeed);

        Player player = context != null ? context.Player : null;

        if (player == null || !player.IsInitialized)
        {
            Debug.LogWarning($"{name}: Init된 Player가 없어 패턴을 시전하지 않습니다.", this);
        }
        else
        {
            Target = player;
        }

        return true;
    }

    protected override void OnInitCompleted()
    {
        base.OnInitCompleted();
        Vital.Died += OnDied;
        Vital.Depleted += OnHealthDepleted;

        if (Target != null)
        {
            Target.Vital.Died += OnTargetDied;
        }

        IdleState.SetDuration(_startIdleDuration);
        stateMachine.Initialize(IdleState);
    }

    protected override void OnRelease()
    {
        Vital.Died -= OnDied;
        Vital.Depleted -= OnHealthDepleted;

        // 타깃이 먼저 파괴된 경우(씬 전환 등)에는 해제할 대상이 없다.
        if (Target != null)
        {
            Target.Vital.Died -= OnTargetDied;
        }

        Target = null;

        if (StatusVfx != null)
        {
            StatusVfx.HideAll();
        }

        base.OnRelease();
    }

    private void Update()
    {
        if (!IsInitialized)
        {
            return;
        }

        OnUpdate();
        stateMachine.LogicUpdate();
    }

    private void FixedUpdate()
    {
        if (!IsInitialized)
        {
            return;
        }

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

    // 페이즈 전환의 체력 충전을 시작한다. 보스별 전환 연출(OnPhaseHealthDepleted)이 끝났을 때 그 상태가 호출한다.
    // 페이즈 전환 중이 아니면 아무것도 하지 않는다.
    public void StartPhaseRefill()
    {
        if (!IsPhaseTransitioning)
        {
            return;
        }

        stateMachine.ChangeState(PhaseRefillState);
    }

    // 체력 충전이 끝났을 때 PhaseRefillState가 호출한다. 다음 페이즈를 시작하고 Idle로 쉬어간 뒤 패턴을 고른다.
    public void CompletePhaseTransition()
    {
        if (!IsPhaseTransitioning)
        {
            return;
        }

        // 체력이 다 찼다는 알림(HealthChanged)을 받는 쪽이 새 페이즈 번호를 읽도록 번호를 먼저 올린다.
        float maxHealth = NextPhaseMaxHealth;
        CurrentPhase++;
        Vital.EndDepletion(maxHealth);
        Vital.SetSurvivesDepletion(HasNextPhase);
        OnPhaseStarted(CurrentPhase);
        ChangeToIdle(_startIdleDuration);

        // 연출 시스템(페이즈 표시 UI 등)용. 새 페이즈의 상태 전환까지 끝난 뒤에 알린다.
        EventBus<EnemyPhaseChangedEvent>.Publish(new EnemyPhaseChangedEvent(gameObject, CurrentPhase));
    }

    // 머리 위에 지속형 상태 VFX(기절, 버프 등)를 켠다. StatusVfxDisplay가 없거나 프리팹이 비어 있으면 아무것도 하지 않는다.
    public void ShowStatusVfx(GameObject prefab)
    {
        if (StatusVfx != null)
        {
            StatusVfx.Show(prefab);
        }
    }

    public void HideStatusVfx(GameObject prefab)
    {
        if (StatusVfx != null)
        {
            StatusVfx.Hide(prefab);
        }
    }

    // 머리 위에 일회성 상태 VFX(감지 등)를 한 번 띄운다.
    public void PlayStatusVfx(GameObject prefab)
    {
        if (StatusVfx != null)
        {
            StatusVfx.Play(prefab);
        }
    }

    // Idle이 끝났을 때 호출된다. 보스마다 다음 패턴을 고르는 규칙이 다르므로 자식 클래스가 구현한다.
    public abstract void ChangeToNextPattern();

    // Idle 중 매 프레임 호출된다. Idle 시간이 끝나기 전에 다른 상태로 바꿔야 하면 바꾸고 true를 반환한다.
    // 기본은 끼어들지 않는다. (플레이어가 죽어 있는 동안에는 호출되지 않는다)
    public virtual bool TryInterruptIdle()
    {
        return false;
    }

    // Idle 시간을 지금 세도 되는지. false인 동안에는 시간이 흐르지 않고, true가 되는 순간부터 센다.
    // 기본은 항상 센다. 땅에 닿은 뒤부터 세야 하는 보스(Mushroom 등)는 오버라이드한다.
    public virtual bool CanCountIdleTime => true;

    // 다음 페이즈가 남은 채로 체력을 모두 잃었을 때 호출된다. 이 보스의 전환 연출을 시작한다.
    // 보스는 오버라이드해 자기 전환 상태(애니메이션·기믹)로 바꾸고, 그 연출이 끝나면 StartPhaseRefill을 호출한다. (base는 호출하지 않는다)
    // 어떤 패턴 도중에도 들어올 수 있으며, 상태를 바꾸면 진행 중이던 패턴은 Exit으로 끊긴다.
    // 기본은 연출 없이 Idle 모습으로 바로 체력을 충전한다.
    protected virtual void OnPhaseHealthDepleted()
    {
        StartPhaseRefill();
        Animation.Play(EnemyAnimation.Idle);
    }

    // 매 프레임 상태 로직(LogicUpdate) 전에 호출된다. 상태와 상관없이 매 프레임 할 일(시간에 따라 차는 패턴 게이지 등)이 있는 보스만 오버라이드한다.
    // 사망·페이즈 전환 중에도 호출되므로 필요하면 보스 쪽에서 확인한다.
    protected virtual void OnUpdate()
    {
    }

    // 새 페이즈가 시작될 때 호출된다. (체력 충전이 끝난 직후, Idle로 가기 전) phase는 새 페이즈 번호(2부터)다.
    // 보스는 오버라이드해 페이즈별 패턴 구성이나 수치를 바꾼다.
    protected virtual void OnPhaseStarted(int phase)
    {
    }

    private void ApplyStats()
    {
        CurrentPhase = 1;
        Vital.Initialize(_stats.MaxHealth);
        Vital.SetSurvivesDepletion(HasNextPhase);

        if (PatternGauge != null)
        {
            PatternGauge.Initialize();
        }
    }

    // 공통 상태를 생성한다. 자식 클래스에서 오버라이드할 때는 반드시 base.CreateStates()를 먼저 호출한다.
    protected virtual void CreateStates()
    {
        stateMachine = new StateMachine<Enemy>();
        IdleState = new EnemyIdleState(this, stateMachine);
        DeadState = new EnemyDeadState(this, stateMachine);
        PhaseRefillState = new EnemyPhaseRefillState(this, stateMachine);
    }

    private void OnDied()
    {
        stateMachine.ChangeState(DeadState);
    }

    // 다음 페이즈가 남은 채로 체력을 모두 잃었을 때. 죽는 대신 페이즈 전환을 시작한다.
    // 패턴 게이지는 페이즈마다 새로 쌓도록 비운다. (전환 중에 기절 가능 표시가 남지 않게 여기서 비운다)
    private void OnHealthDepleted()
    {
        if (PatternGauge != null)
        {
            PatternGauge.SetValue(0f);
        }

        OnPhaseHealthDepleted();
    }

    // 플레이어가 죽으면 진행 중인 패턴을 멈추고 Idle을 유지한다. (IdleState는 타깃이 죽어 있는 동안 다음 패턴으로 넘어가지 않는다.)
    // 페이즈 전환 중이면 끊지 않는다. 전환이 끝나 Idle로 간 뒤 그대로 머문다.
    private void OnTargetDied()
    {
        if (Vital.IsDead || IsPhaseTransitioning)
        {
            return;
        }

        ChangeToIdle(0f);
    }
}

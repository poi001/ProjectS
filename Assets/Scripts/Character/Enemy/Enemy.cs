using UnityEngine;

// 적(보스) 공통 베이스. 컴포넌트 참조와 초기화 순서, 공통 상태(Idle/Dead), 타깃(플레이어) 참조를 관리하고 상태기계를 구동한다.
// 보스마다 패턴이 다르므로 각 보스는 이 클래스를 상속(예: Knight)하고,
// CreateStates를 오버라이드해 고유 패턴 상태를 추가하고 ChangeToNextPattern에서 다음 패턴을 고른다.
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
    [Tooltip("전투 시작 후 첫 패턴을 시전하기 전까지 Idle로 기다리는 시간입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _startIdleDuration = 1f;

    protected StateMachine<Enemy> stateMachine;
    // 부모의 Animation 자동 프로퍼티와 직렬화 필드 이름이 겹치지 않도록 별도 필드에 보관한다.
    private EnemyAnimation _enemyAnimation;

    public CharacterVital Vital { get; private set; }
    public new EnemyAnimation Animation => _enemyAnimation;
    public EnemyStatsSO Stats => _stats;
    public bool IsBoss => _isBoss;
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

        // 타깃이 먼저 파괴된 경우(씬 전환 등)에는 해제할 대상이 없다.
        if (Target != null)
        {
            Target.Vital.Died -= OnTargetDied;
        }

        Target = null;
        base.OnRelease();
    }

    private void Update()
    {
        if (!IsInitialized)
        {
            return;
        }

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

using UnityEngine;

// MineralGolem(광물 골렘) 계열 보스의 공통 베이스. 같은 그림(Golem_1 시트)을 색만 바꿔 쓰는 골렘들이 상속한다. (SapphireGolem, GoldGolem)
// 모션(Idle, Move, Attack, Stun, Dead)과 그 수치가 같으므로 상태와 설정을 여기에 두고, 보스마다 다른 스킬·기믹은 각 자식 클래스에 만든다.
// 1페이즈만 있는 지상형 보스다.
// 패턴을 고르는 규칙이 아직 정해지지 않아, 지금은 모션 테스트용으로 Idle을 사이에 두고 모든 상태를 정해진 순서로 반복한다.
// 흐름: Idle → 이동(Move) → Idle → 공격(Attack) → Idle → 스턴(Stun) → Idle → (처음부터 반복)
//  - 이동: 플레이어 쪽으로 고정된 속도로 걷는다. Move Duration이 지나면 끝난다.
//  - 공격: 제자리에서 팔을 들었다가 주먹으로 바닥을 내리찍는다. 주먹이 닿는 구간에 앞쪽 범위에 판정이 나간다.
//  - 스턴: 정해진 시간 동안 그 자리에 멈추고 머리 위에 공용 기절 VFX를 띄운다.
// 보스별 패턴이 정해지면 자식 클래스에서 ChangeToNextPattern을 오버라이드하고, 고유 상태는 CreateStates를 오버라이드해 추가한다.
// 스턴의 발동 조건은 미정이다. 조건이 정해지면 모션 테스트 순서에서 빼고 ChangeToStun을 그 조건에서 호출한다.
// 플레이 중에는 컴포넌트 컨텍스트 메뉴 "Stun (플레이 중)"으로 바로 확인할 수 있다.
public abstract class MineralGolem : Enemy
{
    [Header("패턴")]
    [Tooltip("상태 하나가 끝난 뒤 다음 상태를 시전하기 전까지 Idle로 쉬는 시간입니다. 전투 시작 때의 Idle 시간은 '시작' 항목의 Start Idle Duration에서 따로 정합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _patternInterval = 1f;

    [Header("이동")]
    [Tooltip("이동 상태에서 플레이어 쪽으로 걷는 속도입니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _moveSpeed = 2f;
    [Tooltip("이동 상태가 유지되는 시간입니다. 이 시간이 지나면 Idle로 쉬어갑니다. 기획이 정해지기 전의 임시값입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _moveDuration = 2f;
    [Tooltip("플레이어와의 가로 거리가 이 값 이하이면 더 다가가지 않고 제자리에서 걷는 모션만 재생합니다. 플레이어와 겹친 채 좌우로 떨리는 것을 막습니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _moveStopDistance = 1.2f;

    [Header("공격")]
    [Tooltip("공격 판정이 시작되는 시점입니다. 애니메이션 진행도(0~1) 기준이며, 주먹을 내리찍기 시작하는 그림(Golem_1_attack 6번)에 맞춘 값입니다.\n클립의 프레임 시간을 바꾸면 함께 맞춰야 합니다.")]
    [SerializeField, Range(0f, 1f)] private float _attackHitStartTime = 0.545f;
    [Tooltip("공격 판정이 끝나는 시점입니다. 애니메이션 진행도(0~1) 기준이며, 주먹이 바닥에 닿은 그림(Golem_1_attack 7번)이 끝나는 시점에 맞춘 값입니다.\n시작~끝 사이에만 판정이 나가며, 이 구간 안에서도 같은 대상은 한 번만 맞습니다.")]
    [SerializeField, Range(0f, 1f)] private float _attackHitEndTime = 0.727f;
    [Tooltip("공격의 피해량 배율입니다. 실제 피해 = 스탯의 공격력 x 이 값입니다. 기획이 정해지기 전의 임시값입니다.\n1 = 공격력 그대로")]
    [SerializeField, Min(0f)] private float _attackDamageMultiplier = 1f;
    [Tooltip("공격 판정 범위입니다. 내리찍은 주먹과 앞쪽으로 튀는 파편이 닿는 영역에 맞춘 값입니다. 보스를 선택하면 Scene 뷰에 범위가 붉은 선으로 보입니다.")]
    [SerializeField] private HitboxShape _attackHitbox = new HitboxShape(new Vector2(0.78f, -0.22f), new Vector2(2.03f, 1.36f));

    [Header("스턴")]
    [Tooltip("스턴에 걸려 멈춰 있는 시간입니다. 기획이 정해지기 전의 임시값입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _stunDuration = 3f;
    [Tooltip("스턴하는 동안 머리 위(StatusVfxDisplay의 Anchor)에 띄울 VFX 프리팹입니다. 모든 보스가 함께 쓰는 Assets/Prefabs/FX/StatusVfx_Stun을 연결합니다. 반복 재생되는 Flipbook과 Manual 반환 PooledObject를 가진 프리팹을 씁니다.\n비워 두면 VFX 없이 스턴합니다.")]
    [SerializeField] private GameObject _stunVfxPrefab;

    // 모션 테스트에서 Idle 사이에 차례로 재생할 상태 목록과 다음에 재생할 번호
    private State<Enemy>[] _motionSequence;
    private int _motionIndex;

    public MineralGolemMoveState MoveState { get; private set; }
    public MineralGolemAttackState AttackState { get; private set; }
    public MineralGolemStunState StunState { get; private set; }

    public float MoveSpeed => _moveSpeed;
    public float MoveDuration => _moveDuration;
    public float MoveStopDistance => _moveStopDistance;
    public float AttackHitStartTime => _attackHitStartTime;
    public float AttackHitEndTime => _attackHitEndTime;
    public float AttackDamage => Stats.AttackPower * _attackDamageMultiplier;
    public HitboxShape AttackHitbox => _attackHitbox;
    public float StunDuration => _stunDuration;
    public GameObject StunVfxPrefab => _stunVfxPrefab;

    protected override bool OnInit(BattleContext context)
    {
        if (!base.OnInit(context))
        {
            return false;
        }

        if (_stunVfxPrefab != null)
        {
            if (StatusVfx == null)
            {
                Debug.LogWarning($"{name} 루트에 StatusVfxDisplay가 없어 스턴 VFX를 띄우지 않습니다.", this);
            }
            else if (PoolManager.HasInstance)
            {
                // 첫 스턴 때 생성 비용으로 끊기지 않도록 미리 하나 만들어 둔다.
                PoolManager.Instance.Prewarm(_stunVfxPrefab, 1);
            }
        }

        return true;
    }

    protected override void CreateStates()
    {
        base.CreateStates();
        MoveState = new MineralGolemMoveState(this, stateMachine);
        AttackState = new MineralGolemAttackState(this, stateMachine);
        StunState = new MineralGolemStunState(this, stateMachine);

        _motionSequence = new State<Enemy>[] { MoveState, AttackState, StunState };
        _motionIndex = 0;
    }

    // Idle 시간이 끝났을 때 호출된다. (플레이어가 죽어 있는 동안에는 호출되지 않는다)
    // 패턴 선택 규칙이 정해지기 전까지는 모션 테스트 순서대로 다음 상태를 재생한다. 보스별 패턴이 정해지면 자식 클래스에서 오버라이드한다.
    public override void ChangeToNextPattern()
    {
        State<Enemy> nextState = _motionSequence[_motionIndex];
        _motionIndex = (_motionIndex + 1) % _motionSequence.Length;
        stateMachine.ChangeState(nextState);
    }

    // 상태가 끝난 뒤 Idle로 쉬어간다. (Pattern Interval)
    public void ChangeToRest()
    {
        ChangeToIdle(_patternInterval);
    }

    // 스턴 조건이 정해지면 그 조건을 확인하는 쪽에서 호출한다. 스턴 상태로 바꿨으면 true를 반환한다.
    // 죽었거나 페이즈 전환 중이면 아무것도 하지 않는다.
    public bool ChangeToStun()
    {
        if (!IsInitialized || Vital.IsDead || IsPhaseTransitioning)
        {
            return false;
        }

        stateMachine.ChangeState(StunState);
        return true;
    }

    [ContextMenu("Stun (플레이 중)")]
    private void PreviewStun()
    {
        ChangeToStun();
    }

    protected override void DrawHitboxGizmos(float facingDirection)
    {
        DrawHitboxGizmo(_attackHitbox, facingDirection, "공격");
    }
}

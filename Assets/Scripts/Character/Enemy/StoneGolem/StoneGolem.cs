using UnityEngine;

// StoneGolem(스톤골렘) 보스. 1페이즈만 있는 지상형 보스다.
// 패턴을 고르는 규칙이 아직 정해지지 않아, 지금은 모션 테스트용으로 Idle을 사이에 두고 모든 상태를 정해진 순서로 반복한다.
// 흐름: Idle → 이동(Move) → Idle → 공격(Attack) → Idle → 스킬(Skill) → Idle → 힐(Heal) → Idle → 스턴(Stun) → Idle → (처음부터 반복)
//  - 이동: 플레이어 쪽으로 고정된 속도로 걷는다. Move Duration이 지나면 끝난다.
//  - 공격: 제자리에서 주먹으로 바닥을 친다. 주먹이 닿는 구간에 앞쪽 좁은 범위에 판정이 나간다.
//  - 스킬: 제자리에서 몸을 일으켜 크게 내려친다. 내려치는 구간에 앞쪽 넓은 범위(궤적)에 판정이 나간다.
//  - 힐: 제자리에서 힐 모션을 재생하고, 몸이 번쩍이는 시점에 최대 체력의 일정 비율을 회복한다.
//  - 스턴: 정해진 시간 동안 그 자리에 멈추고 머리 위에 공용 기절 VFX를 띄운다.
// 힐과 스턴의 발동 조건은 미정이라 실제 패턴에는 연결하지 않았다. 조건이 정해지면 ChangeToHeal / ChangeToStun을 그 조건에서 호출한다.
// 플레이 중에는 컴포넌트 컨텍스트 메뉴 "Heal (플레이 중)", "Stun (플레이 중)"으로 바로 확인할 수 있다.
public class StoneGolem : Enemy
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
    [Tooltip("공격(주먹으로 바닥 치기)의 판정 설정입니다. 판정 구간은 gollux_attack_A의 4 ~ 5번 그림, 범위는 주먹과 파편이 닿는 영역에 맞춘 값입니다.")]
    [SerializeField] private StoneGolemStrike _attack = new StoneGolemStrike(0.235f, 0.353f, 1f, new HitboxShape(new Vector2(0.8f, -0.45f), new Vector2(0.95f, 0.95f)));

    [Header("스킬")]
    [Tooltip("스킬(크게 내려치기)의 판정 설정입니다. 판정 구간은 gollux_attack_B의 5 ~ 6번 그림, 범위는 내려치는 궤적이 닿는 영역에 맞춘 값입니다.")]
    [SerializeField] private StoneGolemStrike _skill = new StoneGolemStrike(0.263f, 0.368f, 1f, new HitboxShape(new Vector2(1.38f, 0.32f), new Vector2(1.7f, 2.5f)));

    [Header("힐")]
    [Tooltip("힐 한 번에 회복하는 양입니다. 최대 체력에 대한 비율이며 0.2 = 최대 체력의 20%입니다. 기획이 정해지기 전의 임시값입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _healRatio = 0.2f;
    [Tooltip("힐 모션에서 체력이 회복되는 시점입니다. 애니메이션 진행도(0~1) 기준이며, 몸이 밝게 번쩍이는 그림(gollux_healing 19번)에 맞춘 값입니다.\n클립의 프레임 시간을 바꾸면 함께 맞춰야 합니다.")]
    [SerializeField, Range(0.01f, 1f)] private float _healTime = 0.76f;

    [Header("스턴")]
    [Tooltip("스턴에 걸려 멈춰 있는 시간입니다. 기획이 정해지기 전의 임시값입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _stunDuration = 3f;
    [Tooltip("스턴하는 동안 머리 위(StatusVfxDisplay의 Anchor)에 띄울 VFX 프리팹입니다. 모든 보스가 함께 쓰는 Assets/Prefabs/FX/StatusVfx_Stun을 연결합니다. 반복 재생되는 Flipbook과 Manual 반환 PooledObject를 가진 프리팹을 씁니다.\n비워 두면 VFX 없이 스턴합니다.")]
    [SerializeField] private GameObject _stunVfxPrefab;

    // 모션 테스트에서 Idle 사이에 차례로 재생할 상태 목록과 다음에 재생할 번호
    private State<Enemy>[] _motionSequence;
    private int _motionIndex;

    public StoneGolemMoveState MoveState { get; private set; }
    public StoneGolemAttackState AttackState { get; private set; }
    public StoneGolemSkillState SkillState { get; private set; }
    public StoneGolemHealState HealState { get; private set; }
    public StoneGolemStunState StunState { get; private set; }

    public float MoveSpeed => _moveSpeed;
    public float MoveDuration => _moveDuration;
    public float MoveStopDistance => _moveStopDistance;
    public StoneGolemStrike Attack => _attack;
    public StoneGolemStrike Skill => _skill;
    public float HealAmount => Vital.MaxHealth * _healRatio;
    public float HealTime => _healTime;
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
                Debug.LogWarning("StoneGolem 루트에 StatusVfxDisplay가 없어 스턴 VFX를 띄우지 않습니다.", this);
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
        MoveState = new StoneGolemMoveState(this, stateMachine);
        AttackState = new StoneGolemAttackState(this, stateMachine);
        SkillState = new StoneGolemSkillState(this, stateMachine);
        HealState = new StoneGolemHealState(this, stateMachine);
        StunState = new StoneGolemStunState(this, stateMachine);

        _motionSequence = new State<Enemy>[] { MoveState, AttackState, SkillState, HealState, StunState };
        _motionIndex = 0;
    }

    // Idle 시간이 끝났을 때 호출된다. (플레이어가 죽어 있는 동안에는 호출되지 않는다)
    // 패턴 선택 규칙이 정해지기 전까지는 모션 테스트 순서대로 다음 상태를 재생한다.
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

    // 힐 조건이 정해지면 그 조건을 확인하는 쪽에서 호출한다. 힐 상태로 바꿨으면 true를 반환한다.
    // 죽었거나 페이즈 전환 중이면 아무것도 하지 않는다.
    public bool ChangeToHeal()
    {
        if (!IsInitialized || Vital.IsDead || IsPhaseTransitioning)
        {
            return false;
        }

        stateMachine.ChangeState(HealState);
        return true;
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

    [ContextMenu("Heal (플레이 중)")]
    private void PreviewHeal()
    {
        ChangeToHeal();
    }

    [ContextMenu("Stun (플레이 중)")]
    private void PreviewStun()
    {
        ChangeToStun();
    }

    protected override void DrawHitboxGizmos(float facingDirection)
    {
        DrawHitboxGizmo(_attack.Hitbox, facingDirection, "공격");
        DrawHitboxGizmo(_skill.Hitbox, facingDirection, "스킬");
    }
}

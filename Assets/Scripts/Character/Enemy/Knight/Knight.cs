using UnityEngine;

// 첫 번째 보스 Knight(임시 이름). GDD 6.6의 상태 구성과 전환 규칙을 따른다.
// 패턴 상태 인스턴스와 패턴 수치를 보관하고, 다음 패턴을 고르는 규칙(사거리 안: 공격 1회/2회, 사거리 밖: 점프/벽타기)을 담당한다.
// 각 패턴의 진행과 종료 판단은 Knight/States/의 상태가 담당한다.
// 수치는 기획에서 정해지지 않은 임시값이며 플레이하면서 인스펙터에서 조정한다. (GDD 9장)
public class Knight : Enemy
{
    [Header("패턴 선택")]
    // 플레이어와의 가로 거리가 이 값 이하이면 사거리 안으로 본다.
    [SerializeField, Min(0f)] private float _attackRange = 3f;
    // 사거리 안에서 공격 2회를 고를 확률 (나머지는 공격 1회)
    [SerializeField, Range(0f, 1f)] private float _comboChance = 0.5f;
    // 사거리 밖에서 벽타기를 고를 확률 (나머지는 점프)
    [SerializeField, Range(0f, 1f)] private float _wallClimbChance = 0.5f;
    // 공격 1회/2회가 끝난 뒤 딜레이 없이 구르기를 시전할 확률
    [SerializeField, Range(0f, 1f)] private float _rollChance = 0.5f;
    // 구르기를 제외한 패턴이 끝난 뒤 Idle로 쉬어갈 확률 (나머지는 바로 다음 패턴)
    [SerializeField, Range(0f, 1f)] private float _idleChance = 0.5f;
    [SerializeField, Min(0f)] private float _idleDuration = 1f;

    [Header("공격")]
    // 공격하면서 플레이어 쪽으로 전진하는 속도
    [SerializeField, Min(0f)] private float _attackStepSpeed = 3f;
    // 한 번 전진하는 길이. 공격 애니메이션 진행도(0~1) 기준이다.
    [SerializeField, Range(0f, 1f)] private float _attackStepLength = 0.25f;
    // 공격 2회에서 두 번째 전진을 시작하는 공격 애니메이션 진행도(0~1)
    [SerializeField, Range(0f, 1f)] private float _comboSecondStepTime = 0.4f;

    [Header("구르기")]
    [SerializeField, Min(0f)] private float _rollSpeed = 6f;

    [Header("점프")]
    // 목표(플레이어, 벽)를 향해 점프할 때 가로 속도의 최대값
    [SerializeField, Min(0f)] private float _jumpMaxSpeedX = 10f;

    [Header("벽타기")]
    [SerializeField, Min(0f)] private float _wallHangDuration = 0.6f;

    [Header("슬라이딩")]
    [SerializeField, Min(0f)] private float _slideSpeed = 12f;
    // 슬라이딩 시작 시점의 플레이어 위치를 이 거리만큼 지나치면 멈춘다.
    [SerializeField, Min(0f)] private float _slideOvershoot = 2f;
    // 위 조건과 관계없이 슬라이딩이 이 시간을 넘기면 멈춘다. (예외 상황 대비)
    [SerializeField, Min(0.1f)] private float _slideMaxDuration = 1.5f;

    // 부모의 Animation 프로퍼티와 이름이 겹치지 않도록 별도 필드에 보관한다.
    private KnightAnimation _knightAnimation;

    public new KnightAnimation Animation => _knightAnimation;

    public KnightAttackState AttackState { get; private set; }
    public KnightComboState ComboState { get; private set; }
    public KnightRollState RollState { get; private set; }
    public KnightJumpState JumpState { get; private set; }
    public KnightWallClimbState WallClimbState { get; private set; }
    public KnightSlideState SlideState { get; private set; }

    public float AttackStepSpeed => _attackStepSpeed;
    public float AttackStepLength => _attackStepLength;
    public float ComboSecondStepTime => _comboSecondStepTime;
    public float RollSpeed => _rollSpeed;
    public float JumpMaxSpeedX => _jumpMaxSpeedX;
    public float WallHangDuration => _wallHangDuration;
    public float SlideSpeed => _slideSpeed;
    public float SlideOvershoot => _slideOvershoot;
    public float SlideMaxDuration => _slideMaxDuration;

    public bool IsTargetInRange => DistanceToTarget <= _attackRange;

    protected override void Awake()
    {
        base.Awake();
        _knightAnimation = GetComponentInChildren<KnightAnimation>();

        if (_knightAnimation == null)
        {
            Debug.LogError("Knight의 Visuals에 KnightAnimation이 없습니다.", this);
            enabled = false;
        }
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
            ChangeToAttackPattern();
            return;
        }

        stateMachine.ChangeState(Random.value < _wallClimbChance ? WallClimbState : JumpState);
    }

    // 공격 1회 또는 공격 2회를 고른다. (사거리 안일 때, 구르기 직후)
    public void ChangeToAttackPattern()
    {
        stateMachine.ChangeState(Random.value < _comboChance ? ComboState : AttackState);
    }

    // 공격 1회/2회가 끝났을 때: 일정 확률로 딜레이 없이 구르기를 시전하고, 아니면 다른 패턴과 같이 처리한다.
    public void ChangeToPatternAfterAttack()
    {
        if (Random.value < _rollChance)
        {
            stateMachine.ChangeState(RollState);
            return;
        }

        ChangeToIdleOrNextPattern();
    }

    // 구르기를 제외한 패턴이 끝났을 때: 일정 확률로 Idle로 쉬어가며 플레이어에게 공격할 시간을 주고, 아니면 바로 다음 패턴을 고른다.
    public void ChangeToIdleOrNextPattern()
    {
        if (Random.value < _idleChance)
        {
            ChangeToIdle(_idleDuration);
            return;
        }

        ChangeToNextPattern();
    }
}

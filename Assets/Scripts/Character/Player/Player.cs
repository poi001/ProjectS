using MoreMountains.Feedbacks;
using UnityEngine;

// 플레이어 컴포넌트 참조와 초기화 순서, 상태 인스턴스를 관리하고 상태기계를 구동한다.
// 상태 전환 판단은 각 상태가, 물리 처리는 CharacterMovement가, 입력과 입력 버퍼는 PlayerInputHandler가 담당한다.
[RequireComponent(typeof(PlayerInputHandler))]
public class Player : Character
{
    // 공격자와 x 차이가 이보다 작으면 좌우를 구분할 수 없는 것으로 본다.
    private const float KnockbackDirectionThreshold = 0.01f;

    [Header("스탯")]
    [Tooltip("플레이어의 기본 스탯 데이터(ScriptableObject)입니다. 최대체력, 공격력, 공격속도, 이동속도, 재사용 대기시간 감소, 최대기력, 치명타 확률, 피해 흡수량, 초당기력회복력의 기본값이 들어 있습니다.\n전투 중의 최종 값은 여기에 % 보정을 적용해 계산합니다. (PlayerStats)")]
    [SerializeField] private PlayerStatsSO _stats;

    [Header("점프")]
    [Tooltip("발판에서 떨어진 뒤에도 이 시간 안에는 점프할 수 있습니다. (코요테 타임, 단위: 초)")]
    [SerializeField] private float _coyoteTime = 0.1f;
    [Tooltip("공중 점프의 높이 배율입니다. 1이면 땅에서 하는 점프와 같은 높이까지 오릅니다. 공중 점프 횟수는 PlayerStatsSO에서 정합니다.")]
    [SerializeField, Min(0f)] private float _airJumpHeightRatio = 1f;
    [Tooltip("공중 점프하는 순간 발밑(몸 콜라이더의 아래쪽 가운데)에 띄우는 일회성 효과 프리팹입니다. Assets/Prefabs/FX/Player/PlayerAirJumpDust를 연결합니다. 띄운 자리에 남고 플레이어를 따라가지 않습니다.\n비워 두면 효과가 나오지 않습니다.")]
    [SerializeField] private GameObject _airJumpDustPrefab;

    [Header("벽타기")]
    [Tooltip("벽을 타는 동안 미끄러져 내려가는 속도입니다. 0이면 벽에 붙은 채 멈춥니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _wallSlideSpeed = 2f;
    [Tooltip("벽을 타는 동안 그림을 벽 쪽으로 옮기는 거리입니다. 벽타기 스프라이트는 그림이 칸 가운데에서 벗어나 있어 그대로 두면 벽에서 떠 보입니다. 늘리면 벽에 더 붙고, 0이면 옮기지 않습니다. 0.33이면 그림의 벽 쪽 끝이 몸 콜라이더 끝과 맞습니다. (단위: 월드 거리)")]
    [SerializeField] private float _wallSlideSpriteOffset = 0.33f;
    [Tooltip("벽 점프 시 벽 반대쪽으로 튀어 나가는 가로 속도입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _wallJumpSpeedX = 7f;
    [Tooltip("벽 점프의 높이 배율입니다. 1이면 땅에서 하는 점프와 같은 높이까지 오릅니다.")]
    [SerializeField, Min(0f)] private float _wallJumpHeightRatio = 1f;
    [Tooltip("벽 점프 직후 좌우 입력을 무시하는 시간입니다. 튀어 나가자마자 같은 벽으로 되돌아가지 않게 합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _wallJumpInputLockTime = 0.15f;
    [Tooltip("벽 점프 후 이 시간 동안은 방금 떠난 벽 쪽으로 이동 키를 눌러도 그쪽으로 움직이거나 벽에 다시 붙지 않습니다. 벽 쪽 키를 누른 채 점프해도 바로 벽에 되돌아가지 않게 합니다. 반대쪽 입력에는 영향이 없습니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _wallJumpWallBlockTime = 0.5f;

    [Header("대시")]
    [Tooltip("대시하는 동안의 가로 속도입니다. 이동 거리는 속도 × 지속 시간입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _dashSpeed = 15f;
    [Tooltip("대시 지속 시간입니다. 이 시간 동안 피격 판정이 꺼져 적의 공격에 걸리지 않습니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _dashDuration = 0.25f;
    [Tooltip("대시가 끝난 뒤 다시 대시할 수 있을 때까지의 시간입니다. 공중에서는 이와 별개로 착지하거나 벽을 탈 때까지 1번만 대시할 수 있습니다. 실제 시간은 재사용 대기시간 감소 스탯만큼 줄어듭니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _dashCooldown = 0.5f;

    [Header("기력")]
    [Tooltip("기력을 소모한 뒤 다시 회복되기 시작할 때까지의 시간입니다. 이 시간 안에 또 소모하면 그때부터 다시 셉니다. 벽을 타는 동안에는 계속 소모하므로 회복되지 않습니다.\n회복 속도는 초당기력회복력 스탯(PlayerStatsSO)을 따릅니다. (임시 값, 단위: 초, 0이면 소모 직후 바로 회복 시작)")]
    [SerializeField, Min(0f)] private float _staminaRegenDelay = 1f;
    [Tooltip("벽타기를 시작할 때(벽에 붙는 순간) 한 번 소모하는 기력입니다. 기력이 이보다 적으면 벽에 붙지 않습니다.")]
    [SerializeField, Min(0f)] private float _wallSlideStartStaminaCost = 10f;
    [Tooltip("벽에 매달려 있는 동안 1초에 걸쳐 연속해서 소모하는 기력입니다. (30이면 0.5초 매달렸을 때 15가 닳음) 기력이 0이 되면 벽에서 떨어집니다. (단위: 기력/초)")]
    [SerializeField, Min(0f)] private float _wallSlideStaminaCostPerSecond = 30f;
    [Tooltip("대시를 시작할 때 한 번 소모하는 기력입니다. 기력이 이보다 적으면 대시할 수 없습니다.")]
    [SerializeField, Min(0f)] private float _dashStaminaCost = 30f;
    [Tooltip("공중 점프를 시작할 때 한 번 소모하는 기력입니다. 기력이 이보다 적으면 공중 점프할 수 없습니다. (땅에서 하는 점프와 벽 점프는 기력을 쓰지 않습니다)")]
    [SerializeField, Min(0f)] private float _airJumpStaminaCost = 30f;
    [Tooltip("블로킹을 시작할 때 한 번 소모하는 기력입니다. 기력이 이보다 적으면 블로킹할 수 없습니다. 막는 데 성공했는지와 관계없이 소모합니다.")]
    [SerializeField, Min(0f)] private float _blockStaminaCost = 30f;

    [Header("공격")]
    [Tooltip("지상 콤보의 단계별 공격 설정입니다. 배열 크기가 곧 콤보 수이며, Animator에 Combo{n}, Combo{n}_End 상태가 이 수만큼 있어야 합니다.\n단계마다 피해 배율과 판정 범위를 정합니다.")]
    [SerializeField] private PlayerAttack[] _comboAttacks =
    {
        new PlayerAttack(1f, new HitboxShape(new Vector2(1f, 0f), new Vector2(1.5f, 1f))),
        new PlayerAttack(1f, new HitboxShape(new Vector2(1f, 0f), new Vector2(1.5f, 1f))),
        new PlayerAttack(1f, new HitboxShape(new Vector2(1f, 0f), new Vector2(1.5f, 1f)))
    };
    [Tooltip("공중 콤보의 단계별 공격 설정입니다. 배열 크기가 곧 공중 콤보 수이며, Animator에 JumpAttack{n}, JumpAttack{n}_End 상태가 이 수만큼 있어야 합니다.\n단계마다 피해 배율과 판정 범위를 정합니다. 공중 콤보는 떨어지면서 나가며, 공중에 있는 동안 횟수 제한 없이 다시 쓸 수 있습니다.")]
    [SerializeField] private PlayerAttack[] _airComboAttacks =
    {
        new PlayerAttack(1f, new HitboxShape(new Vector2(1f, 0f), new Vector2(1.5f, 1f))),
        new PlayerAttack(1f, new HitboxShape(new Vector2(1f, 0f), new Vector2(1.5f, 1f)))
    };

    [Header("피격")]
    [Tooltip("피해를 입은 순간부터 무적인 시간입니다. 넉백 시간도 포함됩니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _invincibleDuration = 1f;
    [Tooltip("피격 시 넉백 속도입니다. x는 피해를 입은 방향의 반대로 밀리는 속도, y는 위로 뜨는 속도입니다. (단위: 월드 거리/초)")]
    [SerializeField] private Vector2 _knockbackVelocity = new Vector2(5f, 5f);
    [Tooltip("실제 피해를 입었을 때 재생하는 Feel 피드백입니다. 짧은 슬로우 모션과 붉은 화면 플래시의 시간·강도는 이 MMF_Player에서 조절합니다. Initialization Mode는 Script로 설정합니다.")]
    [SerializeField] private MMF_Player _hitFeedbacks;

    [Header("블로킹")]
    [Tooltip("블로킹이 끝난 뒤 다시 블로킹할 수 있을 때까지의 시간입니다. 막는 데 성공했는지와 관계없이 적용됩니다. 실제 시간은 재사용 대기시간 감소 스탯만큼 줄어듭니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _blockCooldown = 10f;
    [Tooltip("블로킹에 성공했을 때(공격을 막았을 때) 재생하는 Feel 피드백입니다. 짧은 슬로우 모션의 배율·시간은 이 MMF_Player에서 조절합니다. Initialization Mode는 Script로 설정합니다.\n비워 두면 연출 없이 막기만 합니다.")]
    [SerializeField] private MMF_Player _blockFeedbacks;
    [Tooltip("블로킹에 성공했을 때 재생하는 소리입니다. 비워 두면 소리가 나지 않습니다.")]
    [SerializeField] private SoundDataSO _blockSuccessSound;

    [Header("체력 회복")]
    [Tooltip("체력 회복 동작(위 방향키를 1초 동안 누르고 있기)이 끝났을 때 한 번 회복하는 체력의 양입니다. 임시 값이며, 나중에는 장착한 물약·음식의 회복량으로 바뀝니다. 최대 체력을 넘게 회복되지는 않습니다.")]
    [SerializeField, Min(0f)] private float _healAmount = 30f;

    private StateMachine<Player> _stateMachine;
    // 부모의 Animation 자동 프로퍼티와 직렬화 필드 이름이 겹치지 않도록 별도 필드에 보관한다.
    private PlayerAnimation _playerAnimation;
    // 공중 행동 자원. 공중 점프는 땅에 닿을 때만(RefillAirActions), 공중 대시는 땅에 닿거나 벽을 탈 때(RefillAirDash) 다시 채워진다.
    private int _remainingAirJumps;
    private bool _hasAirDash;
    // 다시 대시할 수 있게 되는 시각 (대시 쿨타임)
    private float _dashReadyTime = float.NegativeInfinity;
    // 다시 블로킹할 수 있게 되는 시각 (블로킹 쿨타임)
    private float _blockReadyTime = float.NegativeInfinity;
    // 다시 스킬을 쓸 수 있게 되는 시각 (스킬 쿨타임)
    private float _skillReadyTime = float.NegativeInfinity;
    // 벽 점프 직후 방금 떠난 벽 쪽 입력을 막는 시각과 그 벽의 방향 (오른쪽 1, 왼쪽 -1)
    private float _wallBlockEndTime = float.NegativeInfinity;
    private float _blockedWallDirection;
    // 입력을 잠그는 이유들. 하나라도 켜져 있으면 조작을 받지 않는다. (RefreshInputLock)
    private bool _isCinematicPlaying;
    private bool _isGamePaused;
    // 시네마틱 중에도 공격만은 받을지 여부. 시네마틱을 연 쪽이 켜고(SetCinematicAttackAllowed), 시네마틱이 끝나면 꺼진다.
    private bool _isCinematicAttackAllowed;
    // 시네마틱이 공격을 허용한 동안 콤보(2타·3타)로 이어갈 수 있는지 여부. 공격을 허용할 때 연 쪽이 함께 정한다. (SetCinematicAttackAllowed)
    private bool _isCinematicComboAllowed = true;
    // 시네마틱이 공격을 허용한 동안 한 번 공격을 시작한 뒤 다음 공격을 시작할 수 있기까지의 간격(초)과, 다음 공격을 시작할 수 있는 시각. 0이면 간격이 없다. (SetCinematicAttackAllowed)
    private float _cinematicAttackInterval;
    private float _nextCinematicAttackTime = float.NegativeInfinity;
    // 시네마틱 중 다른 쪽이 플레이어를 가로로 옮기는 중인지와 그 구간 (StartCinematicSlide)
    private bool _isCinematicSliding;
    private float _cinematicSlideStartX;
    private float _cinematicSlideTargetX;
    private float _cinematicSlideDuration;
    private float _cinematicSlideElapsedTime;
    // 전투 중의 최종 스탯. 기본 스탯(_stats)에 % 보정을 적용한 값이다. Init할 때 새로 만든다. (ApplyStats)
    private PlayerStats _runtimeStats;
    // 현재 기력. 최대량은 최종 스탯의 최대기력을 따른다. 기력바가 계속 구독할 수 있도록 하나만 만들어 두고 Init할 때 가득 채운다. (ApplyStats)
    private readonly PlayerStamina _stamina = new PlayerStamina();
    // 전투 중 장착하고 있는 룬. Init이 끝날 때 장착 정보(PlayerProfile)로 만들고 Release할 때 끝낸다. (OnInitCompleted, OnRelease)
    private readonly PlayerRunes _runes = new PlayerRunes();
    // 기력이 다시 회복되기 시작하는 시각. 기력을 소모할 때마다 회복 지연만큼 뒤로 밀린다. (ConsumeStamina, DrainWallSlideStamina)
    private float _staminaRegenReadyTime = float.NegativeInfinity;
    // 일반 공격의 판정을 내는 중인지 여부와, 그 판정으로 실제로 입힌 피해의 합. 피해 흡수량 계산에 쓴다. (PerformAttackHit)
    private bool _isDealingAttackHit;
    private float _attackHitDamageDealt;

    public PlayerInputHandler InputHandler { get; private set; }
    public new PlayerAnimation Animation => _playerAnimation;
    // 보정까지 적용한 최종 스탯. 전투 중의 수치는 이 값을 쓴다.
    public PlayerStats Stats => _runtimeStats;
    // 현재 기력 (기력바가 표시한다)
    public PlayerStamina Stamina => _stamina;
    // 전투 중 장착하고 있는 룬 (액티브 룬 하나와 발동해 있는 패시브 룬들)
    public PlayerRunes Runes => _runes;
    // 보정을 적용하기 전의 기본 스탯 데이터
    public PlayerStatsSO BaseStats => _stats;
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
    public PlayerBlockState BlockState { get; private set; }
    public PlayerSkillState SkillState { get; private set; }
    public PlayerHealState HealState { get; private set; }
    public PlayerDeadState DeadState { get; private set; }

    // Animator에서 실제로 사용할 수 있는 지상 콤보 수
    public int ComboCount { get; private set; }
    // Animator에서 실제로 사용할 수 있는 공중 콤보 수
    public int AirComboCount { get; private set; }

    public State<Player> CurrentState => _stateMachine.CurrentState;

    // 지금 콤보를 다음 타로 이어갈 수 없는지 여부. 시네마틱이 공격만 허용하면서 콤보는 막은 동안 true다. (PlayerAttackState, PlayerJumpAttackState가 확인한다)
    public bool IsComboBlocked => _isCinematicPlaying && _isCinematicAttackAllowed && !_isCinematicComboAllowed;
    protected override string HurtboxLayerName => "Player";
    protected override string TargetLayerName => "Enemy";

    // 점프 버퍼 시간 안에 입력이 있었고, 코요테 타임 안에 접지했었다면 점프 가능
    public bool CanJump => InputHandler.HasBufferedJump && Movement.TimeSinceGrounded <= _coyoteTime;

    // 공중 점프를 얻었고(Mushroom 처치), 공중에서 점프 입력이 있고 공중 점프 횟수와 기력이 남아 있다면 공중 점프 가능 (코요테 타임 안이라면 CanJump가 먼저 쓰인다)
    public bool CanAirJump => HasAbility(PlayerAbility.AirJump)
        && InputHandler.HasBufferedJump
        && !Movement.IsGrounded
        && _remainingAirJumps > 0
        && _stamina.HasStamina(_airJumpStaminaCost);

    // 대시를 얻어야 하며(SkullDog 처치), 대시 중에는 재진입하지 않고, 쿨타임이 끝난 뒤 새로 누른 입력과 남은 대시 자원·기력이 있어야 한다.
    public bool CanDash => HasAbility(PlayerAbility.Dash)
        && CurrentState != DashState
        && Time.time >= _dashReadyTime
        && InputHandler.IsDashBuffered(_dashReadyTime)
        && (Movement.IsGrounded || _hasAirDash)
        && _stamina.HasStamina(_dashStaminaCost);

    // 벽타기를 시작할 기력(시작 비용)이 있는지 여부. 모자라면 벽에 닿아도 붙지 않는다. (PlayerState.TryChangeToWallSlideState)
    public bool HasWallSlideStartStamina => _stamina.HasStamina(_wallSlideStartStaminaCost);

    // 공중에 있고 Animator에 공중 콤보 상태가 있어야 한다. 횟수 제한은 없다.
    public bool CanAirAttack => !Movement.IsGrounded && AirComboCount > 0;

    // 블로킹을 얻어야 하며(보스 처치, 어느 보스인지 미정), 땅 위에서만 블로킹할 수 있고, 쿨타임이 끝난 뒤 새로 누른 입력과 기력이 있어야 한다.
    // 블로킹 중에는 재진입하지 않는다. (쿨타임은 블로킹이 끝날 때 시작되므로, 끝나는 프레임에 눌러 둔 입력으로 쿨타임 없이 다시 막지 못하게 한다)
    public bool CanBlock => HasAbility(PlayerAbility.Block)
        && CurrentState != BlockState
        && Movement.IsGrounded
        && Time.time >= _blockReadyTime
        && InputHandler.IsBlockBuffered(_blockReadyTime)
        && _stamina.HasStamina(_blockStaminaCost);

    // 스킬은 장착한 액티브 룬이다. 룬을 장착하지 않았으면 스킬 키를 눌러도 아무 일도 없다. (GDD 3.4, 5.2)
    // 땅 위에서만 쓸 수 있고, 쿨타임이 끝난 뒤 새로 누른 입력과 그 룬의 소모 기력이 있어야 한다. 스킬 중에는 재진입하지 않는다. (블로킹과 같은 이유)
    public bool CanSkill => _runes.ActiveRune != null
        && CurrentState != SkillState
        && Movement.IsGrounded
        && Time.time >= _skillReadyTime
        && InputHandler.IsSkillBuffered(_skillReadyTime)
        && _stamina.HasStamina(_runes.ActiveRune.Data.StaminaCost);

    // 회복 아이템(물약·음식)을 장착해야 하며, 땅 위에서 위 방향키를 새로 눌렀고 지금도 누르고 있어야 한다. 체력이 가득 차 있으면 시작하지 않는다.
    // 회복 중에는 재진입하지 않는다. (끝나거나 끊긴 뒤에는 키를 새로 눌러야 다시 시작한다)
    public bool CanHeal => PlayerProfile.Current.IsHealItemEquipped
        && CurrentState != HealState
        && Movement.IsGrounded
        && Vital.CurrentHealth < Vital.MaxHealth
        && InputHandler.HasBufferedHeal
        && InputHandler.IsHealHeld;

    // 보스를 처치해 얻는 동작(GDD 3.6)을 얻었는지 여부. 얻지 못한 동작은 입력이 있어도 시작하지 않는다.
    public bool HasAbility(PlayerAbility ability)
    {
        return PlayerProfile.Current.HasAbility(ability);
    }

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
        _playerAnimation = GetComponentInChildren<PlayerAnimation>();

        if (_playerAnimation == null)
        {
            Debug.LogError("Player의 Visuals에 PlayerAnimation이 없습니다.", this);
            return false;
        }

        // 최종 스탯은 만들 때 스탯 에셋을 구독하므로, 실패(return false)할 수 있는 검사를 모두 마친 뒤에 만든다. (실패한 Init은 Release로 정리되지 않는다)
        ApplyStats();
        CreateStates();

        ComboCount = Animation.SetupCombos(Mathf.Max(1, _comboAttacks.Length));
        AirComboCount = Animation.SetupAirCombos(_airComboAttacks.Length);
        Animation.SetAttackSpeed(_runtimeStats.AttackSpeed);
        ValidateAttackSettings();
        ValidateAnimationStates();
        _hitFeedbacks?.Initialization();

        if (_blockFeedbacks != null)
        {
            _blockFeedbacks.Initialization();
        }

        if (_airJumpDustPrefab != null && PoolManager.HasInstance)
        {
            // 첫 공중 점프 때 생성 비용으로 끊기지 않도록 미리 하나 만들어 둔다.
            PoolManager.Instance.Prewarm(_airJumpDustPrefab, 1);
        }

        _dashReadyTime = float.NegativeInfinity;
        _blockReadyTime = float.NegativeInfinity;
        _skillReadyTime = float.NegativeInfinity;
        _staminaRegenReadyTime = float.NegativeInfinity;
        return true;
    }

    protected override void OnInitCompleted()
    {
        base.OnInitCompleted();
        Vital.Damaged += OnDamaged;
        Vital.Died += OnDied;
        Vital.DeathAverted += OnDeathAverted;
        EventBus<CharacterDiedEvent>.Subscribe(OnCharacterDied);
        EventBus<CinematicStartedEvent>.Subscribe(OnCinematicStarted);
        EventBus<CinematicEndedEvent>.Subscribe(OnCinematicEnded);
        EventBus<GamePauseChangedEvent>.Subscribe(OnGamePauseChanged);
        EventBus<DamageTakenEvent>.Subscribe(OnDamageTaken);
        _runtimeStats.Changed += OnStatChanged;
        // 장착한 룬을 만들고 패시브 룬을 발동한다. 스탯 보정이 체력·이동속도 등에 반영되도록 스탯 구독 뒤에 한다.
        _runes.Init(this, PlayerProfile.Current);
        _isDealingAttackHit = false;
        _isCinematicPlaying = false;
        _isCinematicAttackAllowed = false;
        _isCinematicComboAllowed = true;
        _cinematicAttackInterval = 0f;
        _nextCinematicAttackTime = float.NegativeInfinity;
        _isCinematicSliding = false;
        _isGamePaused = GameManager.HasInstance && GameManager.Instance.IsPaused;
        RefreshInputLock();
        _stateMachine.Initialize(IdleState);
    }

    protected override void OnRelease()
    {
        Animation.StopWallSlideDust();
        Animation.StopDashLines();
        _hitFeedbacks?.StopFeedbacks();

        if (_blockFeedbacks != null)
        {
            _blockFeedbacks.StopFeedbacks();
        }

        Vital.Damaged -= OnDamaged;
        Vital.Died -= OnDied;
        Vital.DeathAverted -= OnDeathAverted;
        EventBus<CharacterDiedEvent>.Unsubscribe(OnCharacterDied);
        EventBus<CinematicStartedEvent>.Unsubscribe(OnCinematicStarted);
        EventBus<CinematicEndedEvent>.Unsubscribe(OnCinematicEnded);
        EventBus<GamePauseChangedEvent>.Unsubscribe(OnGamePauseChanged);
        EventBus<DamageTakenEvent>.Unsubscribe(OnDamageTaken);

        // 룬을 끝내며 스탯 보정이 걷힐 때 체력·애니메이터 등에 다시 넣지 않도록 스탯 구독을 먼저 푼다. (씬이 내려가며 파괴되는 중일 수 있다)
        if (_runtimeStats != null)
        {
            _runtimeStats.Changed -= OnStatChanged;
        }

        _runes.Release();
        ReleaseStats();
        _isDealingAttackHit = false;
        _isCinematicPlaying = false;
        _isCinematicAttackAllowed = false;
        _isCinematicComboAllowed = true;
        _cinematicAttackInterval = 0f;
        _nextCinematicAttackTime = float.NegativeInfinity;
        _isCinematicSliding = false;
        _isGamePaused = false;
        InputHandler.SetLocked(false);
        InputHandler.SetAttackAllowedWhileLocked(false);
        base.OnRelease();
    }

    // 시네마틱 동안에는 조작을 받지 않는다. 하던 동작은 입력이 끊긴 것처럼 자연스럽게 마무리된다. (달리던 중이면 멈추고, 공중이면 착지한다)
    private void OnCinematicStarted(CinematicStartedEvent cinematicEvent)
    {
        _isCinematicPlaying = true;
        _isCinematicAttackAllowed = false;
        _isCinematicComboAllowed = true;
        _cinematicAttackInterval = 0f;
        _nextCinematicAttackTime = float.NegativeInfinity;
        RefreshInputLock();
    }

    // 시네마틱이 끝나면 공격 허용과 가로 이동도 함께 끝낸다. (연 쪽이 따로 끄지 않아도 남지 않게 한다)
    private void OnCinematicEnded(CinematicEndedEvent cinematicEvent)
    {
        _isCinematicPlaying = false;
        _isCinematicAttackAllowed = false;
        _isCinematicComboAllowed = true;
        _cinematicAttackInterval = 0f;
        _nextCinematicAttackTime = float.NegativeInfinity;
        _isCinematicSliding = false;
        RefreshInputLock();
    }

    // 시네마틱 중에도 공격 입력만은 받게 하거나 다시 막는다. 이동·점프·대시는 계속 막힌다. 시네마틱 중이 아니면 아무 효과가 없다.
    // allowsCombo: 공격을 허용하는 동안 콤보(2타·3타)로 이어갈 수 있는지. false면 공격을 눌러도 항상 1타만 나간다. (1타가 끝난 뒤 다시 누르면 다시 1타)
    // 시네마틱을 연 쪽이 호출하며, 콤보를 막을지도 그쪽이 정한다. 시네마틱이 끝나면 공격 허용과 함께 콤보 설정도 기본값(허용)으로 돌아온다.
    // (Mushroom 반응속도 기믹: 제자리에서 1타의 타이밍만 겨루도록 콤보를 막는다)
    // attackInterval: 공격을 시작한 뒤 다음 공격을 시작할 수 있기까지의 간격(초). 공격 동작 길이와 상관없이 이 시간이 지나야 다음 공격이 나간다.
    // 이 동안 누른 공격 입력은 버려진다. (연타로 빠르게 반복하지 못하게 한다) 0이면 간격이 없다.
    // 공격을 새로 허용할 때마다 간격은 새로 시작하므로, 허용한 직후의 첫 공격은 바로 나간다.
    // 간격은 땅 위에서 새 공격을 시작할 때만 적용한다. 콤보 이어가기(allowsCombo)와 공중 공격에는 적용하지 않으므로, 간격을 쓰려면 콤보도 함께 막는다.
    public void SetCinematicAttackAllowed(bool isAllowed, bool allowsCombo, float attackInterval)
    {
        _isCinematicAttackAllowed = isAllowed;
        _isCinematicComboAllowed = allowsCombo;
        _cinematicAttackInterval = Mathf.Max(0f, attackInterval);
        _nextCinematicAttackTime = float.NegativeInfinity;
        RefreshInputLock();
    }

    // 지금 새 공격을 시작할 수 없는지 여부. 시네마틱이 공격 간격을 정해 둔 동안, 마지막 공격을 시작한 뒤 간격이 아직 지나지 않았으면 true다. (PlayerState가 확인한다)
    public bool IsAttackOnCooldown => _isCinematicPlaying && _isCinematicAttackAllowed && Time.time < _nextCinematicAttackTime;

    // 공격을 시작할 때 PlayerAttackState가 호출한다. 시네마틱이 공격 간격을 정해 두었다면 다음 공격을 시작할 수 있는 시각을 정한다. 평소에는 아무것도 하지 않는다.
    public void NotifyAttackStarted()
    {
        if (_isCinematicPlaying && _isCinematicAttackAllowed && _cinematicAttackInterval > 0f)
        {
            _nextCinematicAttackTime = Time.time + _cinematicAttackInterval;
        }
    }

    // 시네마틱 중 플레이어를 targetX까지 duration초에 걸쳐 가로로 부드럽게 옮기고 facingDirection(오른쪽 1, 왼쪽 -1) 쪽을 보게 한다.
    // 시네마틱을 연 쪽이 자리 배치에 쓴다. 조작이 잠겨 있어 자세는 제자리 자세(Idle) 그대로이며, 세로는 중력에 맡긴다.
    // 벽에 막히면 그 자리에서 멈춘 채 시간이 끝난다. 시네마틱이 끝나거나 죽으면 중간에 멈춘다.
    public void StartCinematicSlide(float targetX, float duration, float facingDirection)
    {
        _isCinematicSliding = true;
        _cinematicSlideStartX = Movement.Position.x;
        _cinematicSlideTargetX = targetX;
        _cinematicSlideDuration = Mathf.Max(duration, Time.fixedDeltaTime);
        _cinematicSlideElapsedTime = 0f;
        Animation.UpdateFacing(facingDirection);
    }

    // 상태가 가로 속도를 정한 뒤에 호출해, 이번 물리 스텝의 가로 속도를 자리 배치용 속도로 덮어쓴다. (FixedUpdate)
    // 처음과 끝이 느린 곡선(SmoothStep)으로 움직이며, 다음 스텝의 목표 자리까지 가는 속도를 매번 다시 계산해 오차가 쌓이지 않게 한다.
    private void UpdateCinematicSlide()
    {
        if (Vital.IsDead)
        {
            _isCinematicSliding = false;
            return;
        }

        _cinematicSlideElapsedTime += Time.fixedDeltaTime;
        float progress = Mathf.Clamp01(_cinematicSlideElapsedTime / _cinematicSlideDuration);
        float nextX = Mathf.Lerp(_cinematicSlideStartX, _cinematicSlideTargetX, Mathf.SmoothStep(0f, 1f, progress));
        Movement.SetVelocityX((nextX - Movement.Position.x) / Time.fixedDeltaTime);

        // 마지막 스텝의 속도로 목표 자리에 닿으므로, 그다음 스텝부터는 상태가 정한 속도(0)를 그대로 쓴다.
        if (progress >= 1f)
        {
            _isCinematicSliding = false;
        }
    }

    // 일시정지(옵션 창 등) 동안에는 조작을 받지 않는다. 멈춘 동안 누른 키가 다시 진행될 때 뒤늦게 실행되지 않게 한다.
    private void OnGamePauseChanged(GamePauseChangedEvent pauseEvent)
    {
        _isGamePaused = pauseEvent.IsPaused;
        RefreshInputLock();
    }

    // 시네마틱과 일시정지가 겹칠 수 있으므로, 둘 중 하나라도 진행 중이면 입력을 잠근다.
    // 시네마틱이 공격을 허용한 동안에는 잠근 채로 공격 입력만 받는다. 일시정지 중에는 공격도 받지 않는다.
    private void RefreshInputLock()
    {
        InputHandler.SetLocked(_isCinematicPlaying || _isGamePaused);
        InputHandler.SetAttackAllowedWhileLocked(_isCinematicPlaying && _isCinematicAttackAllowed && !_isGamePaused);
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

        UpdateStaminaRegen();
        _stateMachine.LogicUpdate();
    }

    // 기력을 마지막으로 소모한 뒤 회복 지연이 지났으면 초당기력회복력 스탯만큼 회복한다.
    // 프레임마다 (초당기력회복력 × 지난 시간)만큼 더하므로 1초에 걸쳐 연속해서 찬다. 가득 차 있으면 아무것도 하지 않는다.
    // 게임 시간으로 세므로 일시정지 중에는 멈추고 슬로우 모션 중에는 느려진다. 죽은 뒤에는 회복하지 않는다.
    private void UpdateStaminaRegen()
    {
        if (Vital.IsDead || Time.time < _staminaRegenReadyTime)
        {
            return;
        }

        _stamina.Restore(_runtimeStats.StaminaRegen * Time.deltaTime);
    }

    // 동작을 시작할 때 기력을 한 번 소모하고 회복 지연을 새로 시작한다. 기력이 충분한지는 동작을 시작하기 전에 확인한다. (CanDash 등)
    private void ConsumeStamina(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        _stamina.Drain(amount);
        _staminaRegenReadyTime = Time.time + _staminaRegenDelay;
    }

    // 벽타기를 시작할 때(벽에 붙는 순간) PlayerWallSlideState가 호출한다. 시작 비용을 한 번 소모한다.
    public void ConsumeWallSlideStartStamina()
    {
        ConsumeStamina(_wallSlideStartStaminaCost);
    }

    // 벽에 매달려 있는 동안 PlayerWallSlideState가 프레임마다 호출한다. (초당 소모량 × 지난 시간)만큼 연속해서 소모한다.
    // 기력이 남아 있으면 true, 바닥났으면 false를 반환한다. (false면 벽에서 떨어진다)
    public bool DrainWallSlideStamina(float deltaTime)
    {
        ConsumeStamina(_wallSlideStaminaCostPerSecond * deltaTime);
        return _stamina.CurrentStamina > 0f;
    }

    // 블로킹을 시작할 때 PlayerBlockState가 호출한다.
    public void ConsumeBlockStamina()
    {
        ConsumeStamina(_blockStaminaCost);
    }

    // 스킬을 시작할 때 PlayerSkillState가 호출한다. 장착한 액티브 룬의 소모 기력만큼 소모한다.
    public void ConsumeSkillStamina()
    {
        if (_runes.ActiveRune != null)
        {
            ConsumeStamina(_runes.ActiveRune.Data.StaminaCost);
        }
    }

    // 스킬을 시작할 때 PlayerSkillState가 호출한다. 장착한 액티브 룬을 시전한다. (시전 조건은 CanSkill이 먼저 확인한다)
    public void CastSkill()
    {
        if (_runes.ActiveRune != null)
        {
            _runes.ActiveRune.Cast();
        }
    }

    private void FixedUpdate()
    {
        if (!IsInitialized)
        {
            return;
        }

        _stateMachine.PhysicsUpdate();

        if (_isCinematicSliding)
        {
            UpdateCinematicSlide();
        }
    }

    // 공중 점프 횟수와 공중 대시를 다시 채운다. 땅에 닿아 있을 때만 호출된다. (벽을 타도 공중 점프는 채워지지 않는다)
    public void RefillAirActions()
    {
        // 공중 점프 횟수는 % 보정을 받는 스탯이 아니라 기본 스탯 데이터의 횟수를 그대로 쓴다.
        _remainingAirJumps = _stats.AirJumpCount;
        RefillAirDash();
    }

    // 벽 점프를 시작할 때 호출한다. 지금부터 정해진 시간 동안 그 벽 쪽 이동 입력을 무시한다.
    public void BlockWallDirection(float wallDirection)
    {
        _blockedWallDirection = wallDirection >= 0f ? 1f : -1f;
        _wallBlockEndTime = Time.time + _wallJumpWallBlockTime;
    }

    // 막힌 벽 쪽 이동 입력이면 0으로, 아니면 입력을 그대로 돌려준다.
    public float FilterMoveInput(float moveInput)
    {
        if (Time.time < _wallBlockEndTime && moveInput * _blockedWallDirection > 0f)
        {
            return 0f;
        }

        return moveInput;
    }

    // 공중 대시만 다시 채운다. 벽을 타기 시작할 때 호출된다.
    public void RefillAirDash()
    {
        _hasAirDash = true;
    }

    // 공중 점프를 시작할 때 호출한다. 공중 점프 횟수와 기력을 소모한다.
    public void ConsumeAirJump()
    {
        _remainingAirJumps = Mathf.Max(0, _remainingAirJumps - 1);
        ConsumeStamina(_airJumpStaminaCost);
    }

    // 공중 점프를 시작할 때 PlayerAirJumpState가 호출한다. 발밑 자리에 효과(pixelFX impacts/air-jump, Mushroom과 같은 그림)를 띄운다.
    // 풀에서 꺼낸 효과는 부모 없이 그 자리에 남고, 재생이 끝나면 Flipbook이 풀로 돌려보낸다. 좌우 대칭이라 뒤집지 않는다.
    public void SpawnAirJumpDust()
    {
        if (_airJumpDustPrefab == null || !PoolManager.HasInstance)
        {
            return;
        }

        Bounds bodyBounds = BodyCollider.bounds;
        Vector3 position = new Vector3(bodyBounds.center.x, bodyBounds.min.y, 0f);
        PoolManager.Instance.Spawn(_airJumpDustPrefab, position, Quaternion.identity);
    }

    // 대시를 시작할 때 호출한다. 기력을 소모하고, 공중에서 시작했다면 공중 대시도 소모한다.
    public void ConsumeDash()
    {
        if (!Movement.IsGrounded)
        {
            _hasAirDash = false;
        }

        ConsumeStamina(_dashStaminaCost);
    }

    // 대시가 끝날 때 호출한다. 지금부터 쿨타임이 지나야 다시 대시할 수 있다.
    public void StartDashCooldown()
    {
        _dashReadyTime = Time.time + GetReducedCooldown(_dashCooldown);
    }

    // 블로킹이 끝날 때 호출한다. 지금부터 쿨타임이 지나야 다시 블로킹할 수 있다.
    public void StartBlockCooldown()
    {
        _blockReadyTime = Time.time + GetReducedCooldown(_blockCooldown);
    }

    // 스킬이 끝날 때 호출한다. 지금부터 장착한 액티브 룬의 쿨타임이 지나야 다시 스킬을 쓸 수 있다.
    public void StartSkillCooldown()
    {
        float cooldown = _runes.ActiveRune != null ? _runes.ActiveRune.Data.Cooldown : 0f;
        _skillReadyTime = Time.time + GetReducedCooldown(cooldown);
    }

    // 재사용 대기시간 감소 스탯(%)을 적용한 실제 대기시간. 재사용 대기시간이 있는 동작(대시, 블로킹, 스킬, 추후 물약)은 모두 이 값을 쓴다.
    // 대기시간을 시작하는 순간의 스탯으로 정하며, 이미 돌고 있는 대기시간은 스탯이 바뀌어도 그대로다.
    public float GetReducedCooldown(float cooldown)
    {
        return cooldown * (1f - _runtimeStats.CooldownReduction / 100f);
    }

    // 체력 회복 동작이 끝까지 재생되었을 때 호출한다. 정해진 양만큼 체력을 회복한다. (피격 등으로 끊기면 호출되지 않는다)
    public void CompleteHeal()
    {
        Vital.Heal(_healAmount);
    }

    // 블로킹으로 공격을 막았을 때 호출한다. 짧은 슬로우 모션(피드백)과 소리를 재생한다.
    // 막았을 때 적에게 주는 불이익이나 플레이어가 얻는 이득은 기획이 정해지면 이 자리에 추가한다.
    public void PlayBlockSuccessEffects()
    {
        if (_blockFeedbacks != null)
        {
            _blockFeedbacks.PlayFeedbacks();
        }

        if (_blockSuccessSound != null && SoundManager.HasInstance)
        {
            SoundManager.Instance.PlaySfx(_blockSuccessSound);
        }
    }

    // 해당 지상 콤보 단계의 공격 설정. 설정되지 않았다면 null을 반환한다.
    public PlayerAttack GetComboAttack(int comboIndex)
    {
        if (comboIndex >= _comboAttacks.Length)
        {
            return null;
        }

        return _comboAttacks[comboIndex];
    }

    // 해당 공중 콤보 단계의 공격 설정. 설정되지 않았다면 null을 반환한다.
    public PlayerAttack GetAirComboAttack(int comboIndex)
    {
        if (comboIndex >= _airComboAttacks.Length)
        {
            return null;
        }

        return _airComboAttacks[comboIndex];
    }

    // 공격의 데미지 (공격력 × 공격 배율). 치명타는 포함하지 않는다.
    public float GetDamage(PlayerAttack attack)
    {
        return _runtimeStats.AttackPower * attack.DamageMultiplier;
    }

    // 일반 공격(지상·공중 콤보)의 타격 한 번을 판정한다. 공격 상태가 타격 프레임마다 호출한다.
    // 타격마다 치명타 확률 스탯으로 치명타를 굴리고, 치명타면 피해에 치명타 배율(기본 2배)을 곱한다. 한 번의 타격에 여러 대상이 맞으면 모두 같은 결과다.
    // 이 타격으로 실제로 입힌 피해 중 피해 흡수량 스탯(%)만큼 체력을 회복한다.
    public void PerformAttackHit(PlayerAttack attack)
    {
        bool isCritical = RollCritical();
        float damage = GetDamage(attack);

        if (isCritical)
        {
            damage *= _stats.CritDamageMultiplier;
        }

        _attackHitDamageDealt = 0f;
        _isDealingAttackHit = true;

        try
        {
            HitChecker.ResetHitTargets();
            HitChecker.Hit(attack.Hitbox, damage, Animation.FacingDirection, isCritical);
        }
        finally
        {
            _isDealingAttackHit = false;
        }

        float healAmount = _attackHitDamageDealt * _runtimeStats.LifeSteal / 100f;

        if (healAmount > 0f)
        {
            Vital.Heal(healAmount);
        }
    }

    // 치명타 확률 스탯(%)으로 이번 타격이 치명타인지 정한다.
    private bool RollCritical()
    {
        float critChance = _runtimeStats.CritChance;
        return critChance > 0f && (critChance >= 100f || Random.value * 100f < critChance);
    }

    // 일반 공격의 판정을 내는 동안 이 플레이어가 입힌 피해를 모은다. (피해 흡수량 계산용) 최소 체력·남은 체력에 걸려 깎이지 않은 피해는 포함되지 않는다.
    private void OnDamageTaken(DamageTakenEvent damageEvent)
    {
        if (_isDealingAttackHit && damageEvent.Instigator == gameObject)
        {
            _attackHitDamageDealt += damageEvent.Amount;
        }
    }

    // 선택했을 때 지상 콤보와 공중 콤보의 단계별 범위를 그린다.
    protected override void DrawHitboxGizmos(float facingDirection)
    {
        if (_comboAttacks != null)
        {
            for (int i = 0; i < _comboAttacks.Length; i++)
            {
                DrawHitboxGizmo(_comboAttacks[i].Hitbox, facingDirection, $"콤보 {i + 1}");
            }
        }

        if (_airComboAttacks != null)
        {
            for (int i = 0; i < _airComboAttacks.Length; i++)
            {
                DrawHitboxGizmo(_airComboAttacks[i].Hitbox, facingDirection, $"공중 콤보 {i + 1}");
            }
        }
    }

    // 기본 스탯으로 최종 스탯을 새로 만들고 이동속도와 최대 체력·최대 기력에 넣는다. (기력은 가득 채운다) (공격속도는 Animator 준비가 끝난 뒤 OnInit에서 넣는다)
    // 스탯이 바뀌었을 때 다시 넣는 것은 OnStatChanged가 한다. 구독은 OnInitCompleted에서 한다.
    private void ApplyStats()
    {
        ReleaseStats();
        _runtimeStats = new PlayerStats(_stats);
        Movement.SetMoveSpeed(_runtimeStats.MoveSpeed);
        Vital.Initialize(_runtimeStats.MaxHealth);
        _stamina.Initialize(_runtimeStats.MaxStamina);
        // 같은 프레임에 보스도 죽으면 플레이어가 이기므로, 치명 피해의 사망 확정을 프레임 끝까지 미룬다. (GDD 6.4)
        Vital.SetDefersDeath(true);
    }

    // 최종 스탯의 구독을 풀고 정리한다. 값은 Release 뒤에도 읽을 수 있게 남겨 둔다. (다음 Init에서 새로 만든다)
    private void ReleaseStats()
    {
        if (_runtimeStats == null)
        {
            return;
        }

        _runtimeStats.Changed -= OnStatChanged;
        _runtimeStats.Dispose();
    }

    // 전투 중 스탯이 바뀌면(룬·장비 보정 등) 다른 컴포넌트에 넣어 둔 값을 다시 넣는다. 공격력·치명타 확률처럼 쓸 때마다 읽는 스탯은 할 일이 없다.
    private void OnStatChanged(PlayerStatType statType)
    {
        switch (statType)
        {
            case PlayerStatType.MaxHealth:
                Vital.SetMaxHealth(_runtimeStats.MaxHealth);
                break;
            case PlayerStatType.AttackSpeed:
                Animation.SetAttackSpeed(_runtimeStats.AttackSpeed);
                break;
            case PlayerStatType.MoveSpeed:
                Movement.SetMoveSpeed(_runtimeStats.MoveSpeed);
                break;
            case PlayerStatType.MaxStamina:
                _stamina.SetMaxStamina(_runtimeStats.MaxStamina);
                break;
        }
    }

    // 콤보 공격 설정이 비어 있으면 지상 공격이 판정을 내지 못하므로 알린다.
    private void ValidateAttackSettings()
    {
        if (_comboAttacks.Length == 0)
        {
            Debug.LogError("지상 콤보 공격 설정이 비어 있습니다. 공격해도 판정이 나가지 않습니다.", this);
        }
    }

    // 애니메이션이 끝나야 빠져나가는 상태(JumpStart, Hit, WallJump, Block, Skill1, Heal)는 Animator에 상태가 없으면 끝나지 않으므로 미리 알린다.
    // 나머지(Fall, WallSlide, Dash)는 상태가 없어도 멈추지는 않지만 모습이 바뀌지 않으므로 함께 확인한다.
    private void ValidateAnimationStates()
    {
        ValidateAnimationState(PlayerAnimation.JumpStart, "JumpStart");
        ValidateAnimationState(PlayerAnimation.Fall, "Fall");
        ValidateAnimationState(PlayerAnimation.Hit, "Hit");
        ValidateAnimationState(PlayerAnimation.WallSlide, "WallSlide");
        ValidateAnimationState(PlayerAnimation.WallJump, "WallJump");
        ValidateAnimationState(PlayerAnimation.Dash, "Dash");
        ValidateAnimationState(PlayerAnimation.Block, "Block");
        ValidateAnimationState(PlayerAnimation.Skill1, "Skill1");
        ValidateAnimationState(PlayerAnimation.Heal, "Heal");
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
        BlockState = new PlayerBlockState(this, _stateMachine);
        SkillState = new PlayerSkillState(this, _stateMachine);
        HealState = new PlayerHealState(this, _stateMachine);
        DeadState = new PlayerDeadState(this, _stateMachine);
    }

    // 피해를 입으면 무적을 시작하고 진행 중이던 동작을 끊고 피격 상태로 전환한다.
    private void OnDamaged(DamageInfo damageInfo)
    {
        // 치명 피해에도 화면 효과를 재생한다. 이어지는 KO 연출은 Feel의 시간 배율 스택 위에 적용된다.
        _hitFeedbacks?.PlayFeedbacks();

        // 치명 피해는 Died에서 사망 상태로 전환하므로 피격 상태를 거치지 않는다.
        if (Vital.IsDead)
        {
            return;
        }

        ApplyHitReaction(damageInfo);
    }

    // 같은 프레임에 보스도 죽어 치명 피해로 죽지 않고 살아남았을 때. 치명 피해를 맞은 순간 건너뛴 피격 반응(무적, 피격 상태)을 지금 한다. (GDD 6.4)
    private void OnDeathAverted(DamageInfo damageInfo)
    {
        ApplyHitReaction(damageInfo);
    }

    private void ApplyHitReaction(DamageInfo damageInfo)
    {
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

    // 보스가 죽으면 승부가 났으므로 이후 피해로 체력이 1 아래로 내려가지 않게 한다. 잡몹의 죽음은 승부와 무관하다. (GDD 6.4)
    private void OnCharacterDied(CharacterDiedEvent diedEvent)
    {
        if (diedEvent.Target != null && diedEvent.Target.TryGetComponent(out Enemy enemy) && enemy.IsBoss)
        {
            Vital.SetOpponentDead();
        }
    }
}

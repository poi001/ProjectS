using MoreMountains.Feedbacks;
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
    [Tooltip("대시가 끝난 뒤 다시 대시할 수 있을 때까지의 시간입니다. 공중에서는 이와 별개로 착지하거나 벽을 탈 때까지 1번만 대시할 수 있습니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _dashCooldown = 1f;

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
    [Tooltip("블로킹이 끝난 뒤 다시 블로킹할 수 있을 때까지의 시간입니다. 막는 데 성공했는지와 관계없이 적용됩니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _blockCooldown = 10f;
    [Tooltip("블로킹에 성공했을 때(공격을 막았을 때) 재생하는 Feel 피드백입니다. 짧은 슬로우 모션의 배율·시간은 이 MMF_Player에서 조절합니다. Initialization Mode는 Script로 설정합니다.\n비워 두면 연출 없이 막기만 합니다.")]
    [SerializeField] private MMF_Player _blockFeedbacks;
    [Tooltip("블로킹에 성공했을 때 재생하는 소리입니다. 비워 두면 소리가 나지 않습니다.")]
    [SerializeField] private SoundDataSO _blockSuccessSound;

    [Header("스킬")]
    [Tooltip("스킬(스페이스바)이 끝난 뒤 다시 쓸 수 있을 때까지의 시간입니다. 0이면 끝나자마자 다시 쓸 수 있습니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _skillCooldown = 0f;

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
    // 마지막으로 벽 점프한 벽. 이 벽에는 다시 벽타기를 할 수 없다. 땅에 닿으면 비우고, 다른 벽에서 벽 점프하면 그 벽으로 바뀐다.
    private Collider2D _lastWallJumpedWall;
    // 입력을 잠그는 이유들. 하나라도 켜져 있으면 조작을 받지 않는다. (RefreshInputLock)
    private bool _isCinematicPlaying;
    private bool _isGamePaused;
    // 시네마틱 중에도 공격만은 받을지 여부. 시네마틱을 연 쪽이 켜고(SetCinematicAttackAllowed), 시네마틱이 끝나면 꺼진다.
    private bool _isCinematicAttackAllowed;
    // 시네마틱이 공격을 허용한 동안 콤보(2타·3타)로 이어갈 수 있는지 여부. 공격을 허용할 때 연 쪽이 함께 정한다. (SetCinematicAttackAllowed)
    private bool _isCinematicComboAllowed = true;
    // 시네마틱 중 다른 쪽이 플레이어를 가로로 옮기는 중인지와 그 구간 (StartCinematicSlide)
    private bool _isCinematicSliding;
    private float _cinematicSlideStartX;
    private float _cinematicSlideTargetX;
    private float _cinematicSlideDuration;
    private float _cinematicSlideElapsedTime;

    public PlayerInputHandler InputHandler { get; private set; }
    public CharacterVital Vital { get; private set; }
    public new PlayerAnimation Animation => _playerAnimation;
    public PlayerStatsSO Stats => _stats;
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

    // 공중에서 점프 입력이 있고 공중 점프 횟수가 남아 있다면 공중 점프 가능 (코요테 타임 안이라면 CanJump가 먼저 쓰인다)
    public bool CanAirJump => InputHandler.HasBufferedJump && !Movement.IsGrounded && _remainingAirJumps > 0;

    // 대시 중에는 재진입하지 않으며, 쿨타임이 끝난 뒤 새로 누른 입력과 남은 대시 자원이 있어야 한다.
    public bool CanDash => CurrentState != DashState
        && Time.time >= _dashReadyTime
        && InputHandler.IsDashBuffered(_dashReadyTime)
        && (Movement.IsGrounded || _hasAirDash);

    // 공중에 있고 Animator에 공중 콤보 상태가 있어야 한다. 횟수 제한은 없다.
    public bool CanAirAttack => !Movement.IsGrounded && AirComboCount > 0;

    // 땅 위에서만 블로킹할 수 있고, 쿨타임이 끝난 뒤 새로 누른 입력이 있어야 한다.
    // 블로킹 중에는 재진입하지 않는다. (쿨타임은 블로킹이 끝날 때 시작되므로, 끝나는 프레임에 눌러 둔 입력으로 쿨타임 없이 다시 막지 못하게 한다)
    public bool CanBlock => CurrentState != BlockState
        && Movement.IsGrounded
        && Time.time >= _blockReadyTime
        && InputHandler.IsBlockBuffered(_blockReadyTime);

    // 땅 위에서만 스킬을 쓸 수 있고, 쿨타임이 끝난 뒤 새로 누른 입력이 있어야 한다. 스킬 중에는 재진입하지 않는다. (블로킹과 같은 이유)
    public bool CanSkill => CurrentState != SkillState
        && Movement.IsGrounded
        && Time.time >= _skillReadyTime
        && InputHandler.IsSkillBuffered(_skillReadyTime);

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
        AirComboCount = Animation.SetupAirCombos(_airComboAttacks.Length);
        Animation.SetAttackSpeed(_stats.AttackSpeed);
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
        return true;
    }

    protected override void OnInitCompleted()
    {
        base.OnInitCompleted();
        Vital.Damaged += OnDamaged;
        Vital.Died += OnDied;
        EventBus<CinematicStartedEvent>.Subscribe(OnCinematicStarted);
        EventBus<CinematicEndedEvent>.Subscribe(OnCinematicEnded);
        EventBus<GamePauseChangedEvent>.Subscribe(OnGamePauseChanged);
        _isCinematicPlaying = false;
        _isCinematicAttackAllowed = false;
        _isCinematicComboAllowed = true;
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
        EventBus<CinematicStartedEvent>.Unsubscribe(OnCinematicStarted);
        EventBus<CinematicEndedEvent>.Unsubscribe(OnCinematicEnded);
        EventBus<GamePauseChangedEvent>.Unsubscribe(OnGamePauseChanged);
        _isCinematicPlaying = false;
        _isCinematicAttackAllowed = false;
        _isCinematicComboAllowed = true;
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
        RefreshInputLock();
    }

    // 시네마틱이 끝나면 공격 허용과 가로 이동도 함께 끝낸다. (연 쪽이 따로 끄지 않아도 남지 않게 한다)
    private void OnCinematicEnded(CinematicEndedEvent cinematicEvent)
    {
        _isCinematicPlaying = false;
        _isCinematicAttackAllowed = false;
        _isCinematicComboAllowed = true;
        _isCinematicSliding = false;
        RefreshInputLock();
    }

    // 시네마틱 중에도 공격 입력만은 받게 하거나 다시 막는다. 이동·점프·대시는 계속 막힌다. 시네마틱 중이 아니면 아무 효과가 없다.
    // allowsCombo: 공격을 허용하는 동안 콤보(2타·3타)로 이어갈 수 있는지. false면 공격을 눌러도 항상 1타만 나간다. (1타가 끝난 뒤 다시 누르면 다시 1타)
    // 시네마틱을 연 쪽이 호출하며, 콤보를 막을지도 그쪽이 정한다. 시네마틱이 끝나면 공격 허용과 함께 콤보 설정도 기본값(허용)으로 돌아온다.
    // (Mushroom 반응속도 기믹: 제자리에서 1타의 타이밍만 겨루도록 콤보를 막는다)
    public void SetCinematicAttackAllowed(bool isAllowed, bool allowsCombo)
    {
        _isCinematicAttackAllowed = isAllowed;
        _isCinematicComboAllowed = allowsCombo;
        RefreshInputLock();
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

        // 땅에 닿아 있는 동안 공중 점프와 공중 대시를 다시 채우고, 벽 점프한 벽 기록을 지운다.
        if (Movement.IsGrounded)
        {
            RefillAirActions();
            _lastWallJumpedWall = null;
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

        if (_isCinematicSliding)
        {
            UpdateCinematicSlide();
        }
    }

    // 공중 점프 횟수와 공중 대시를 다시 채운다. 땅에 닿아 있을 때만 호출된다. (벽을 타도 공중 점프는 채워지지 않는다)
    public void RefillAirActions()
    {
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

    // 벽 점프를 시작할 때 호출한다. 이 벽에는 땅에 닿거나 다른 벽에서 벽 점프하기 전까지 다시 벽타기를 할 수 없다.
    public void SetLastWallJumpedWall(Collider2D wall)
    {
        _lastWallJumpedWall = wall;
    }

    // 마지막으로 벽 점프한 벽인지 확인한다. 기록이 없으면 false.
    public bool IsLastWallJumpedWall(Collider2D wall)
    {
        return _lastWallJumpedWall != null && wall == _lastWallJumpedWall;
    }

    // 공중 대시만 다시 채운다. 벽을 타기 시작할 때 호출된다.
    public void RefillAirDash()
    {
        _hasAirDash = true;
    }

    public void ConsumeAirJump()
    {
        _remainingAirJumps = Mathf.Max(0, _remainingAirJumps - 1);
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

    // 블로킹이 끝날 때 호출한다. 지금부터 쿨타임이 지나야 다시 블로킹할 수 있다.
    public void StartBlockCooldown()
    {
        _blockReadyTime = Time.time + _blockCooldown;
    }

    // 스킬이 끝날 때 호출한다. 지금부터 쿨타임이 지나야 다시 스킬을 쓸 수 있다.
    public void StartSkillCooldown()
    {
        _skillReadyTime = Time.time + _skillCooldown;
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

    // 공격의 데미지 (공격력 × 공격 배율)
    public float GetDamage(PlayerAttack attack)
    {
        return _stats.AttackPower * attack.DamageMultiplier;
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

    // 애니메이션이 끝나야 빠져나가는 상태(JumpStart, Hit, WallJump, Block, Skill1)는 Animator에 상태가 없으면 끝나지 않으므로 미리 알린다.
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

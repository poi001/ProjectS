using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

// 두 번째 보스 SoulKnight. Enemy_GDD 6장의 상태 구성과 전환 규칙을 따른다.
// 현재 구현 범위는 1페이즈 각성 전 패턴이다. Idle이 끝나면 아래 두 묶음 중 하나를 고르고, 묶음이 끝나면 Idle로 쉬어간다.
//  - 구르기 → 슬라이딩: 가장 먼 벽으로 구른 뒤(무적) 반대쪽 벽까지 미끄러지고, 벽에 부딪히면 포물선으로 튕겨 맵 중앙에 떨어진다.
//  - 점프 → 점프공격: 플레이어 머리 위로 뛰어오른 뒤 수직으로 내려찍어 착지 지점 주변에 범위 피해를 준다.
// 패턴 게이지와 기절은 Knight와 같다. 슬라이딩·점프공격을 시작할 때 차고 플레이어를 맞히면 줄며, 꽉 차도 패턴을 계속하다가
// 플레이어에게 피해를 입는 순간 기절한다. 기절이 끝나면 일어나기를 거쳐 Idle로 쉬어간다. (Enemy_GDD 5.4, 6.7)
// 각 패턴의 진행과 종료 판단은 SoulKnight/States/의 상태가 담당한다.
// Idle 중 체력이 50% 이하이면 전투당 한 번 그 자리에서 5초 기도한다. (피해 무효, Enemy_GDD 6.8) 기도가 끝나면 Idle 뒤 기존 패턴을 이어간다.
// 각성 후 행동, 2페이즈, Attack·Health·CrouchIdle은 기획이 정해지지 않아 모션 테스트로만 재생한다.
// 수치는 기획에서 정해지지 않은 임시값이며 플레이하면서 인스펙터에서 조정한다.
[RequireComponent(typeof(PatternGauge))]
public sealed class SoulKnight : Enemy
{
    // 좌우 벽을 찾을 때 검사하는 최대 거리
    private const float WallSearchDistance = 100f;
    // 벽 앞에서 멈출 때 콜라이더 가장자리와 벽 사이에 남기는 간격 (단위: 월드 거리)
    private const float WallGap = 0.02f;
    // 두 거리의 차이가 이 값 이하이면 같은 거리로 본다. (구르기에서 벽을 고를 때, 단위: 월드 거리)
    private const float SameDistanceTolerance = 0.1f;
    // 각성 전에 피해로 내려갈 수 있는 최소 체력. 플레이어의 피해가 커도 기도에 들어가기 전에 죽지 않도록 1을 남긴다.
    private const float PreAwakeningMinHealth = 1f;

    private readonly Dictionary<SoulKnightMotion, SoulKnightMotionState> _states = new();
    private static readonly SoulKnightMotion[] _previewSequence =
    {
        SoulKnightMotion.Attack1, SoulKnightMotion.Jump, SoulKnightMotion.Sliding,
        SoulKnightMotion.Praying, SoulKnightMotion.Health, SoulKnightMotion.Roll,
        SoulKnightMotion.CrouchIdle, SoulKnightMotion.Death
    };

    [Header("패턴 선택")]
    [Tooltip("Idle이 끝난 뒤 점프→점프공격을 고를 확률입니다. 나머지 확률은 구르기→슬라이딩입니다.\n0 = 항상 구르기→슬라이딩, 1 = 항상 점프→점프공격")]
    [SerializeField, Range(0f, 1f)] private float _jumpPatternChance = 0.5f;
    [Tooltip("패턴 묶음이 하나 끝난 뒤 Idle로 쉬어가는 시간입니다. Idle 애니메이션 길이(현재 1초)에 맞춘 값이므로 클립 길이를 바꾸면 함께 맞춥니다. (단위: 초)\n전투 시작 직후의 첫 Idle은 위쪽 '시작' 항목의 값을 씁니다.")]
    [SerializeField, Min(0f)] private float _idleDuration = 1f;

    [Header("슬라이딩 (구르기 뒤)")]
    [Tooltip("슬라이딩으로 바닥을 미끄러지는 가로 속도입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _slideSpeed = 20f;
    [Tooltip("슬라이딩 중 몸에 닿은 플레이어가 입는 피해량 배율입니다. 실제 피해 = SoulKnight 공격력 × 이 값입니다. 1 = 공격력 그대로")]
    [SerializeField, Min(0f)] private float _slideDamageMultiplier = 1f;
    [Tooltip("슬라이딩 중 플레이어가 몸에 닿았는지 확인하는 판정 범위입니다. 누워서 미끄러지는 모습(Sliding 클립의 불투명 영역)에 맞춘 가로로 긴 직사각형입니다.\nSoulKnight 위치 기준이며 오프셋 x는 미끄러지는 방향 기준입니다. 한 번의 슬라이딩에서 한 번만 맞습니다.")]
    [SerializeField] private HitboxShape _slideBodyHitbox = new HitboxShape(new Vector2(0.7f, -0.65f), new Vector2(2.2f, 1.2f));
    [Tooltip("Sliding 클립에서 일어나는 모습이 시작되는 지점입니다. 이 지점 직전의 누운 모습에서 멈춘 채 미끄러지고 튕겨나며, 멈춘 뒤에 이 지점부터 끝까지 재생합니다. (애니메이션 진행도 0~1)\n현재 클립은 10프레임 중 9번째 프레임부터 일어나므로 0.8입니다. 클립의 프레임을 바꾸면 함께 맞춥니다.")]
    [SerializeField, Range(0f, 1f)] private float _slideGetUpProgress = 0.8f;
    [Tooltip("슬라이딩이 반대쪽 벽에 부딪힌 뒤 맵 중앙(좌우 벽의 가운데)을 향해 포물선으로 튕겨날 때 올라가는 최고 높이입니다. (단위: 월드 거리, 바닥 기준)\n낮을수록 체공 시간이 짧아 빠르게 날아가고, 높을수록 높이 떠올라 천천히 날아갑니다. 중앙에 착지하도록 가로 속도는 자동으로 계산합니다.\n0이면 튕겨나지 않고 벽 앞에서 일어납니다.")]
    [SerializeField, Min(0f)] private float _slideBounceHeight = 2f;

    [Header("점프")]
    [Tooltip("점프로 뛰어오르는 높이입니다. 최고점에서 플레이어 머리 위에 도착합니다. (단위: 월드 거리, 바닥에 선 위치 기준)\n최고점에서 머리가 보스 체력바 바로 아래에 오도록 맞춘 값입니다.")]
    [SerializeField, Min(0.5f)] private float _jumpHeight = 5f;

    [Header("점프공격 (점프 뒤)")]
    [Tooltip("점프공격으로 수직으로 내려찍는 속도입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _jumpAttackFallSpeed = 25f;
    [Tooltip("점프공격이 바닥에 닿을 때 주는 피해량 배율입니다. 실제 피해 = SoulKnight 공격력 × 이 값입니다. 1 = 공격력 그대로")]
    [SerializeField, Min(0f)] private float _jumpAttackDamageMultiplier = 1f;
    [Tooltip("점프공격이 바닥에 닿을 때 한 번 터지는 충격의 판정 범위입니다. SoulKnight 위치 기준이며 착지 모습의 먼지가 퍼지는 영역에 맞춘 값입니다.")]
    [SerializeField] private HitboxShape _jumpAttackImpactHitbox = new HitboxShape(new Vector2(0f, -0.5f), new Vector2(4.6f, 1.5f));
    [Tooltip("JumpAttack 클립에서 착지 모습이 시작되는 지점입니다. 이 지점 직전의 낙하 모습에서 멈춘 채 떨어지고, 바닥에 닿으면 이 지점부터 재생합니다. (애니메이션 진행도 0~1)\n현재 클립은 8프레임 중 4번째 프레임부터 착지이므로 0.375입니다.")]
    [SerializeField, Range(0f, 1f)] private float _jumpAttackLandProgress = 0.375f;
    [Tooltip("JumpAttack 클립의 착지 모습이 끝나는 지점입니다. 여기에 도달하면 Idle로 갑니다. (애니메이션 진행도 0~1)\n현재 클립의 마지막(8번째) 프레임은 빈 그림이라 그 앞에서 끝나도록 0.875로 둡니다. 클립을 고치면 1로 바꿉니다.")]
    [SerializeField, Range(0f, 1f)] private float _jumpAttackEndProgress = 0.875f;

    [Header("벽 충돌·착지 연출")]
    [Tooltip("슬라이딩이 벽에 부딪힐 때와 점프공격이 바닥에 닿을 때 재생할 효과음 데이터입니다. SoundManager의 Sfx 채널로 재생하며, 비워 두면 소리 없이 진행합니다.")]
    [SerializeField] private SoundDataSO _impactSound;
    [Tooltip("슬라이딩이 벽에 부딪힐 때와 점프공격이 바닥에 닿을 때 한 번 재생할 Feel 피드백입니다. (카메라 흔들림) 시간·세기는 피드백에서 조절합니다.")]
    [SerializeField] private MMF_Player _impactFeedbacks;

    // 패턴 게이지는 SoulKnight가 지친 정도다. Knight와 같은 규칙이며 양은 게이지 최대값(PatternGauge) 기준이다.
    [Header("패턴 게이지 (지침)")]
    [Tooltip("슬라이딩 또는 점프공격(내려찍기)을 시작할 때 게이지가 차는 양입니다. (최대값 100 기준이면 퍼센트)\nKnight의 드롭킥·슬라이딩과 같은 값입니다. 구르기와 점프에서는 차지 않습니다.")]
    [SerializeField, Min(0f)] private float _patternGaugeGain = 15f;
    [Tooltip("슬라이딩 몸 충돌, 점프공격 충격이 플레이어에게 피해를 입힐 때마다 게이지가 줄어드는 양입니다. (최대값 100 기준이면 퍼센트)\nKnight와 같은 값입니다.")]
    [SerializeField, Min(0f)] private float _hitGaugeReduction = 20f;

    [Header("기절")]
    [Tooltip("게이지가 꽉 찬 상태에서 플레이어에게 피해를 입어 기절했을 때 멈춰 있는 시간입니다. 이 시간 동안 게이지가 서서히 줄어 끝날 때 0이 됩니다. (단위: 초)\n기절 애니메이션(Death 클립)보다 길면 마지막 프레임을 유지합니다.")]
    [SerializeField, Min(0f)] private float _stunDuration = 3f;
    [Tooltip("기절하는 동안 머리 위(StatusVfxDisplay의 Anchor)에 띄울 VFX 프리팹입니다. 반복 재생되는 Flipbook과 Manual 반환 PooledObject를 가진 프리팹을 씁니다.\n비워 두면 VFX 없이 기절합니다.")]
    [SerializeField] private GameObject _stunVfxPrefab;
    [Tooltip("패턴 게이지가 꽉 찬 상태에서 플레이어에게 피해를 입어 기절할 때 한 번 재생할 효과음 데이터입니다. SoundManager의 Sfx 채널로 재생하며, 비워 두면 소리가 나지 않습니다.")]
    [SerializeField] private SoundDataSO _stunSound;

    [Header("기절 가능 표시")]
    [Tooltip("패턴 게이지가 꽉 차 플레이어의 다음 피격으로 기절할 수 있을 때 재생하는 피드백입니다. All In 1 Sprite Shader의 파란 외곽선 머티리얼로 바꿉니다.")]
    [SerializeField] private MMF_Player _stunReadyFeedbacks;
    [Tooltip("기절 가능 표시가 끝났을 때 재생하는 피드백입니다. 게이지 감소, 기절 진입, 전투 종료, Release 시 원래 머티리얼로 되돌립니다.")]
    [SerializeField] private MMF_Player _stunReadyEndFeedbacks;
    [Tooltip("게이지가 꽉 찬 상태에서 피격되어 기절할 때 한 번 재생하는 외피 파괴 이펙트입니다. Knight와 같은 프리팹을 씁니다.\n피격 순간의 모습을 복사해 청백색으로 번쩍인 뒤 파란 외피와 파편이 사라집니다. 비워 두면 이펙트 없이 기절합니다.")]
    [SerializeField] private KnightStunBreakEffect _stunBreakEffectPrefab;

    [Header("기도 (체력 50% 이하)")]
    [Tooltip("각성 전 Idle 중 현재 체력이 최대 체력의 이 비율 이하이면 각성하고 기도를 시작합니다. 각성은 전투당 한 번이라 기도도 한 번만 합니다.\n각성 전에는 체력이 1 아래로 내려가지 않으므로 큰 피해로 기도 전에 죽지 않습니다.\n0.5 = 체력 50% 이하")]
    [SerializeField, Range(0f, 1f)] private float _prayingHealthRatio = 0.5f;
    [Tooltip("그 자리에서 기도 자세로 있는 시간입니다. 이 동안 플레이어의 공격은 맞지만 피해는 0입니다. 끝나면 Idle로 쉰 뒤 기존 패턴을 이어갑니다. (단위: 초)\nPraying 애니메이션(1.5초)보다 길면 마지막 프레임을 유지합니다.")]
    [SerializeField, Min(0f)] private float _prayingDuration = 5f;

    [Header("모션 테스트")]
    [Tooltip("켜면 전투 패턴 대신 Idle 사이에 모든 모션을 순서대로 시험합니다. 공격 4타와 점프·점프공격은 이어서 재생하며 사망은 모션만 미리 봅니다.\n끄면 실제 전투 패턴을 시전합니다.")]
    [SerializeField] private bool _autoPreview;
    private int _previewIndex;
    private SoulKnightAnimation _soulKnightAnimation;
    private bool _isStunReadyEffectActive;

    public bool AutoPreview => _autoPreview;
    // 1페이즈 각성 여부. 체력이 기도 비율(50%) 이하인 것을 Idle에서 처음 확인해 기도에 들어가는 순간 true가 된다. 전투가 시작되면 false다.
    // 각성 전에는 피해로 체력이 1 아래로 내려가지 않고(PreAwakeningMinHealth), 각성한 뒤에는 제한이 없어진다.
    // 기도는 각성할 때 한 번만 하므로, 이후 회복 등으로 체력이 다시 50% 위로 올랐다 내려와도 다시 기도하지 않는다.
    public bool IsAwakened { get; private set; }

    public SoulKnightRollState RollState { get; private set; }
    public SoulKnightSlideState SlideState { get; private set; }
    public SoulKnightJumpState JumpState { get; private set; }
    public SoulKnightJumpAttackState JumpAttackState { get; private set; }
    public SoulKnightStunState StunState { get; private set; }
    public SoulKnightStunRecoverState StunRecoverState { get; private set; }
    public SoulKnightPrayingState PrayingState { get; private set; }

    public float PrayingDuration => _prayingDuration;
    public float PatternGaugeGain => _patternGaugeGain;
    public float StunDuration => _stunDuration;
    public GameObject StunVfxPrefab => _stunVfxPrefab;

    public float SlideSpeed => _slideSpeed;
    public float SlideDamage => Stats.AttackPower * _slideDamageMultiplier;
    public HitboxShape SlideBodyHitbox => _slideBodyHitbox;
    public float SlideGetUpProgress => _slideGetUpProgress;
    public float SlideBounceHeight => _slideBounceHeight;
    public float JumpHeight => _jumpHeight;
    public float JumpAttackFallSpeed => _jumpAttackFallSpeed;
    public float JumpAttackDamage => Stats.AttackPower * _jumpAttackDamageMultiplier;
    public HitboxShape JumpAttackImpactHitbox => _jumpAttackImpactHitbox;
    public float JumpAttackLandProgress => _jumpAttackLandProgress;
    public float JumpAttackEndProgress => _jumpAttackEndProgress;

#if UNITY_EDITOR
    [Tooltip("플레이 중 컴포넌트 메뉴에서 시험할 모션입니다. 공격은 선택한 타수부터 이어집니다.")]
    [SerializeField] private SoulKnightMotion _testMotion = SoulKnightMotion.Attack1;

    [ContextMenu("선택한 모션 테스트")]
    private void PreviewMotion()
    {
        if (Application.isPlaying && IsInitialized && !Vital.IsDead && !IsTargetDead)
        {
            PlayMotion(_testMotion);
        }
    }
#endif

    protected override bool OnInit(BattleContext context)
    {
        if (!base.OnInit(context))
        {
            return false;
        }

        _soulKnightAnimation = GetComponentInChildren<SoulKnightAnimation>();
        IsAwakened = false;
        Vital.SetMinimumHealth(PreAwakeningMinHealth);

        if (PatternGauge == null)
        {
            Debug.LogError("SoulKnight 루트에 PatternGauge가 없습니다. SoulKnight는 패턴 게이지로 기절을 판단합니다.", this);
            return false;
        }

        if (_soulKnightAnimation == null)
        {
            Debug.LogError("SoulKnight의 Visuals에 SoulKnightAnimation이 없습니다.", this);
            return false;
        }

        if (_stunVfxPrefab != null)
        {
            if (StatusVfx == null)
            {
                Debug.LogWarning("SoulKnight 루트에 StatusVfxDisplay가 없어 기절 VFX를 띄우지 않습니다.", this);
            }
            else if (PoolManager.HasInstance)
            {
                // 첫 기절 때 생성 비용으로 끊기지 않도록 미리 하나 만들어 둔다.
                PoolManager.Instance.Prewarm(_stunVfxPrefab, 1);
            }
        }

        if (_stunBreakEffectPrefab != null && PoolManager.HasInstance)
        {
            PoolManager.Instance.Prewarm(_stunBreakEffectPrefab.gameObject, 1);
        }

        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.Initialization();
        }

        return true;
    }

    protected override void CreateStates()
    {
        base.CreateStates();
        RollState = new SoulKnightRollState(this, stateMachine);
        SlideState = new SoulKnightSlideState(this, stateMachine);
        JumpState = new SoulKnightJumpState(this, stateMachine);
        JumpAttackState = new SoulKnightJumpAttackState(this, stateMachine);
        StunState = new SoulKnightStunState(this, stateMachine);
        StunRecoverState = new SoulKnightStunRecoverState(this, stateMachine);
        PrayingState = new SoulKnightPrayingState(this, stateMachine);

        _previewIndex = 0;
        _states.Clear();
        foreach (SoulKnightMotion motion in System.Enum.GetValues(typeof(SoulKnightMotion)))
        {
            _states.Add(motion, new SoulKnightMotionState(this, stateMachine, motion));
        }
    }

    protected override void OnInitCompleted()
    {
        base.OnInitCompleted();
        Vital.Damaged += OnDamaged;
        Vital.Died += RefreshStunReadyEffect;
        PatternGauge.Changed += RefreshStunReadyEffect;

        if (Target != null)
        {
            Target.Vital.Died += RefreshStunReadyEffect;
        }

        RefreshStunReadyEffect();

        if (!IsTargetDead)
        {
            FaceTarget();
        }
    }

    // Idle이 끝났을 때: 점프→점프공격 또는 구르기→슬라이딩을 확률로 고른다. 모션 테스트를 켰다면 시험 순서를 따른다.
    public override void ChangeToNextPattern()
    {
        if (Vital.IsDead || IsTargetDead)
        {
            return;
        }

        if (_autoPreview)
        {
            PlayMotion(_previewSequence[_previewIndex]);
            _previewIndex = (_previewIndex + 1) % _previewSequence.Length;
            return;
        }

        if (Random.value < _jumpPatternChance)
        {
            stateMachine.ChangeState(JumpState);
        }
        else
        {
            stateMachine.ChangeState(RollState);
        }
    }

    protected override void OnRelease()
    {
        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.StopFeedbacks();
        }

        Vital.Damaged -= OnDamaged;
        Vital.Died -= RefreshStunReadyEffect;
        PatternGauge.Changed -= RefreshStunReadyEffect;

        if (Target != null)
        {
            Target.Vital.Died -= RefreshStunReadyEffect;
        }

        SetStunReadyEffect(false);

        // 패턴 도중에 Release되면 상태의 Exit이 호출되지 않으므로, 상태가 바꿔 둔 피격 판정·피해 무효·중력을 여기서 되돌린다.
        SetHurtboxEnabled(true);
        Vital.SetDamageNullified(false);
        Movement.SetGravityEnabled(true);
        base.OnRelease();
    }

    internal void PlayMotion(SoulKnightMotion motion)
    {
        stateMachine.ChangeState(_states[motion]);
    }

    // 각성 전에 Idle 중 체력이 정해진 비율 이하로 내려와 있으면 각성하고 기도를 시작한다. 각성은 전투당 한 번이며, 모션 테스트 중에는 하지 않는다.
    // 패턴·기절 중에 체력이 내려가도 바로 끊지 않고, 그 패턴이 끝나 Idle로 돌아왔을 때 기도한다.
    // 그동안 체력은 PreAwakeningMinHealth(1) 아래로 내려가지 않아, 큰 피해로 기도 전에 죽지 않는다.
    public override bool TryInterruptIdle()
    {
        if (IsAwakened || _autoPreview || Vital.IsDead || Vital.CurrentHealth > Vital.MaxHealth * _prayingHealthRatio)
        {
            return false;
        }

        Awaken();
        stateMachine.ChangeState(PrayingState);
        return true;
    }

    // 각성한다. 각성 전 최소 체력 보호를 풀어 이후부터는 피해로 죽을 수 있다. (기도 중에는 피해 무효이므로 기도가 끝난 뒤부터 적용된다)
    private void Awaken()
    {
        IsAwakened = true;
        Vital.SetMinimumHealth(0f);
    }

    // 패턴 묶음이 끝났을 때: Idle로 쉬어가며 플레이어에게 공격할 시간을 준다. Idle이 끝나면 다음 패턴을 고른다.
    public void ChangeToRest()
    {
        ChangeToIdle(_idleDuration);
    }

    // 구르기가 끝났을 때 호출한다. direction(오른쪽 1, 왼쪽 -1) 쪽으로 슬라이딩을 시작한다.
    public void ChangeToSlide(float direction)
    {
        SlideState.SetDirection(direction);
        stateMachine.ChangeState(SlideState);
    }

    // 구르기로 붙을 벽의 방향(오른쪽 1, 왼쪽 -1)을 고른다. 좌우 어디에도 벽이 없으면 false를 반환한다.
    // 자신과 가장 먼 벽을 고르고, 두 벽까지의 거리가 같으면 플레이어와 먼 벽을, 플레이어도 두 벽과 같은 거리에 있으면 오른쪽 벽을 고른다.
    public bool TryChooseRollWall(out float direction)
    {
        bool hasRight = TryGetWallX(1f, out float rightX);
        bool hasLeft = TryGetWallX(-1f, out float leftX);
        direction = 1f;

        if (!hasRight && !hasLeft)
        {
            return false;
        }

        if (hasRight != hasLeft)
        {
            direction = hasRight ? 1f : -1f;
            return true;
        }

        float positionX = BodyCollider.bounds.center.x;
        float rightDistance = rightX - positionX;
        float leftDistance = positionX - leftX;

        if (Mathf.Abs(rightDistance - leftDistance) > SameDistanceTolerance)
        {
            direction = rightDistance > leftDistance ? 1f : -1f;
            return true;
        }

        float targetX = Target.transform.position.x;
        float targetToRight = rightX - targetX;
        float targetToLeft = targetX - leftX;

        if (Mathf.Abs(targetToRight - targetToLeft) > SameDistanceTolerance)
        {
            direction = targetToRight > targetToLeft ? 1f : -1f;
        }

        return true;
    }

    // 해당 방향(오른쪽 1, 왼쪽 -1)의 벽에 몸이 밀착할 때까지 남은 거리. 그쪽에 벽이 없으면 0을 반환한다.
    public float GetBodyDistanceToWall(float direction)
    {
        Bounds bounds = BodyCollider.bounds;

        if (!TryGetWallX(direction, out float wallX))
        {
            return 0f;
        }

        return Mathf.Max(0f, Mathf.Abs(wallX - bounds.center.x) - bounds.extents.x - WallGap);
    }

    // x 위치를 몸이 좌우 벽과 겹치지 않는 범위로 제한한다. (점프 목표 지점용)
    // x는 물리 위치(Movement.Position) 기준이다. 콜라이더 중심이 위치와 어긋나 있어도 맞도록 몸 중심으로 바꿔 제한한 뒤 되돌린다.
    public float ClampInsideWalls(float x)
    {
        Bounds bounds = BodyCollider.bounds;
        float margin = bounds.extents.x + WallGap;
        float centerOffset = bounds.center.x - Movement.Position.x;
        float centerX = x + centerOffset;

        if (TryGetWallX(-1f, out float leftX))
        {
            centerX = Mathf.Max(centerX, leftX + margin);
        }

        if (TryGetWallX(1f, out float rightX))
        {
            centerX = Mathf.Min(centerX, rightX - margin);
        }

        return centerX - centerOffset;
    }

    // 맵 중앙(좌우 벽의 가운데)의 x 위치를 구한다. 한쪽이라도 벽이 없으면 false를 반환한다.
    public bool TryGetMapCenterX(out float centerX)
    {
        bool hasRight = TryGetWallX(1f, out float rightX);
        bool hasLeft = TryGetWallX(-1f, out float leftX);
        centerX = (leftX + rightX) * 0.5f;
        return hasRight && hasLeft;
    }

    // 슬라이딩의 벽 충돌과 점프공격의 착지가 함께 쓰는 충격 효과음과 카메라 흔들림을 재생한다. 패턴이 중간에 끊긴 경우에는 호출하지 않는다.
    public void PlayImpact()
    {
        if (_impactSound != null && SoundManager.HasInstance)
        {
            SoundManager.Instance.PlaySfx(_impactSound);
        }

        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.PlayFeedbacks();
        }
    }

    // 기절에 진입할 때 한 번만 재생한다. 기절 중 추가 피격으로는 호출되지 않는다.
    public void PlayStunSound()
    {
        if (_stunSound == null || !SoundManager.HasInstance)
        {
            return;
        }

        SoundManager.Instance.PlaySfx(_stunSound);
    }

    // 슬라이딩 몸 충돌, 점프공격 충격이 플레이어를 맞혔을 때 호출한다. 맞힌 수만큼 게이지를 줄인다.
    public void ReducePatternGaugeByHits(int hitCount)
    {
        if (hitCount <= 0)
        {
            return;
        }

        PatternGauge.Reduce(_hitGaugeReduction * hitCount);
    }

    // 실제로 다음 피격에 기절할 수 있는 동안만 표시한다. 기절 진입 시에는 게이지가 아직 꽉 차 있어도 끈다.
    public void RefreshStunReadyEffect()
    {
        bool isReady = IsInitialized && !Vital.IsDead && !IsTargetDead && PatternGauge.IsFull
            && CurrentState != StunState && CurrentState != StunRecoverState && CurrentState != PrayingState;
        SetStunReadyEffect(isReady);
    }

    private void SetStunReadyEffect(bool isActive)
    {
        if (_isStunReadyEffectActive == isActive)
        {
            return;
        }

        _isStunReadyEffectActive = isActive;
        MMF_Player feedbacks = isActive ? _stunReadyFeedbacks : _stunReadyEndFeedbacks;

        if (feedbacks != null)
        {
            feedbacks.PlayFeedbacks();
        }
    }

    // 게이지가 꽉 찬 상태에서 플레이어에게 피해를 입으면 진행 중인 패턴을 끊고 즉시 기절한다.
    private void OnDamaged(DamageInfo damageInfo)
    {
        // 치명 피해는 사망을 우선하며, 기절 중 추가 피격으로 기절 시간을 초기화하지 않는다.
        if (Vital.IsDead || !PatternGauge.IsFull || CurrentState == StunState || CurrentState == StunRecoverState)
        {
            return;
        }

        if (Target == null || damageInfo.Instigator != Target.gameObject)
        {
            return;
        }

        // 기절 애니메이션으로 바뀌기 전에 피격 순간의 외형을 복사한다.
        if (_stunBreakEffectPrefab != null && PoolManager.HasInstance)
        {
            GameObject effect = PoolManager.Instance.Spawn(_stunBreakEffectPrefab.gameObject, transform.position, Quaternion.identity);

            if (effect != null)
            {
                effect.GetComponent<KnightStunBreakEffect>().Play(_soulKnightAnimation.SpriteRenderer, damageInfo.HitPoint);
            }
        }

        stateMachine.ChangeState(StunState);
    }

    // 선택했을 때 슬라이딩 몸 충돌 범위와 점프공격 충격 범위를 그린다.
    protected override void DrawHitboxGizmos(float facingDirection)
    {
        DrawHitboxGizmo(_slideBodyHitbox, facingDirection, "슬라이딩 몸 충돌");
        DrawHitboxGizmo(_jumpAttackImpactHitbox, facingDirection, "점프공격 충격");
    }

    // 몸 중심 높이에서 해당 방향(오른쪽 1, 왼쪽 -1)에 있는 벽 안쪽 면의 x 위치를 구한다. 벽이 없으면 false를 반환한다.
    private bool TryGetWallX(float direction, out float wallX)
    {
        Vector2 origin = BodyCollider.bounds.center;
        float distance = Movement.GetFreeDistanceToWall(origin, direction, WallSearchDistance);
        wallX = origin.x + Mathf.Sign(direction) * distance;
        return distance < WallSearchDistance;
    }
}

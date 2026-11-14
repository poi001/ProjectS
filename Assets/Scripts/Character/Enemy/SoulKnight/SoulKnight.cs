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
// 각성 후에는 위 두 묶음에 공격 4타(딜레이 → 1타 → 2타 → 찌르기 → 내려치기, 사거리 밖이면 딜레이 → 찌르기부터)를 더해 세 묶음 중 하나를 고른다. (Enemy_GDD 6.9)
// 1페이즈의 체력을 모두 잃으면 죽지 않고 1페이즈 다운(쓰러짐, 시네마틱 5초) → 체력 충전 → 2페이즈로 넘어간다. (Enemy_GDD 1.1, 6.10)
// 1페이즈 다운(쓰러짐)부터 몸에서 계속 피를 흘리며, 2페이즈 내내 사망 상태에서도 이어진다. (출혈 효과)
// 2페이즈 패턴은 각성 후 세 묶음에 회복(Health)을 더한 네 묶음이다. 회복은 모션이 끝나면 체력을 회복하고, 모션 중 피해를 입으면 그 순간 끊겨 Idle로 간다. (Enemy_GDD 6.12)
// 2페이즈의 패턴 게이지는 시간으로 차고, 꽉 차면 기도로 이어진다. (Enemy_GDD 6.11) CrouchIdle은 모션 테스트로만 재생한다.
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
    // 2페이즈 기도 기믹 동안 보스가 쓰는 레이어. Ground 말고는 어떤 레이어와도 충돌하지 않는다. (솟아오르는 기둥에 밀리지 않고 플레이어의 공격도 통과한다)
    private const string GhostLayerName = "Ghost";

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

    // 각성 후에는 세 묶음 중 하나를 가중치 비율로 고른다. 세 값이 같으면 각 1/3이다.
    // 2페이즈에서는 회복(Health) 가중치가 더해져 네 묶음 중 하나를 고른다. 네 값이 같으면 각 25%다.
    [Header("패턴 선택 (각성 후)")]
    [Tooltip("각성 후 Idle이 끝났을 때 구르기→슬라이딩을 고르는 가중치입니다. 세 가중치의 비율로 확률이 정해집니다. (세 값이 같으면 각 1/3)\n0이면 고르지 않습니다.")]
    [SerializeField, Min(0f)] private float _awakenedRollWeight = 1f;
    [Tooltip("각성 후 Idle이 끝났을 때 점프→점프공격을 고르는 가중치입니다. 세 가중치의 비율로 확률이 정해집니다.\n0이면 고르지 않습니다.")]
    [SerializeField, Min(0f)] private float _awakenedJumpWeight = 1f;
    [Tooltip("각성 후 Idle이 끝났을 때 공격 4타를 고르는 가중치입니다. 세 가중치의 비율로 확률이 정해집니다.\n공격 사거리 안이면 1타부터, 밖이면 찌르기(3타)부터 시작합니다. 0이면 고르지 않습니다.")]
    [SerializeField, Min(0f)] private float _awakenedAttackWeight = 1f;
    [Tooltip("2페이즈에서 Idle이 끝났을 때 회복(Health)을 고르는 가중치입니다. 위 세 가중치와 합친 네 값의 비율로 확률이 정해집니다. (네 값이 같으면 각 25%)\n1페이즈에서는 회복을 고르지 않습니다. 2페이즈라도 체력이 가득 차 있으면 회복을 빼고 나머지 셋 중에서 고릅니다. 0이면 고르지 않습니다.")]
    [SerializeField, Min(0f)] private float _phase2HealthWeight = 1f;

    // 각성 후 공격 4타: Attack1 → Attack2 → Thrust(찌르기) → Smash(내려치기)를 딜레이 없이 잇는다. 사거리 밖이면 Thrust부터 시작한다.
    // Attack1·Attack2·Smash의 판정 시점은 각 클립에 넣은 애니메이션 이벤트(OnAttackHitFrame)가 정한다.
    [Header("공격 (공통)")]
    [Tooltip("플레이어와의 가로 거리가 이 값 이하이면 사거리 안으로 봅니다. 공격 4타를 고를 때 사거리 안이면 1타부터, 밖이면 찌르기(3타)부터 시작합니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _attackRange = 3f;
    [Tooltip("1타·2타를 휘두르면서 전진하는 가로 속도입니다. 각 휘두름의 전진 구간 동안만 전진합니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _attackStepSpeed = 3f;
    [Tooltip("공격 4타의 각 공격(1타·2타·찌르기·내려치기)을 시작할 때마다 패턴 게이지가 차는 양입니다. (최대값 100 기준이면 퍼센트)")]
    [SerializeField, Min(0f)] private float _attackGaugeGain = 10f;

    [Header("공격 1타·2타 (휘두르기)")]
    [Tooltip("1타의 판정 범위·피해 배율·전진 구간입니다. 판정 범위는 Attack1 클립에서 칼이 가장 멀리 나간 프레임에 맞춘 임시값입니다.\n전진 구간 0.29~0.57은 칼을 휘두르는 3~4번째 프레임입니다.")]
    [SerializeField] private SoulKnightSwing _attack1Swing = new SoulKnightSwing(new HitboxShape(new Vector2(1f, 0.07f), new Vector2(2f, 2.6f)), 1f, 0.29f, 0.57f);
    [Tooltip("2타의 판정 범위·피해 배율·전진 구간입니다. 판정 범위는 Attack2 클립에서 칼이 가장 멀리 나간 프레임에 맞춘 임시값입니다.\n전진 구간 0.5~1은 다시 휘두르는 3~4번째 프레임입니다.")]
    [SerializeField] private SoulKnightSwing _attack2Swing = new SoulKnightSwing(new HitboxShape(new Vector2(1.25f, 0f), new Vector2(2.5f, 2.5f)), 1f, 0.5f, 1f);

    [Header("공격 3타 (찌르기)")]
    [Tooltip("찌르는 동안 앞으로 돌진하는 거리입니다. 맵 크기와 관계없는 고정 거리이며, 현재 맵 가로 폭의 절반 정도로 맞춘 값입니다. (단위: 월드 거리)\n벽에 막히면 이동만 멈추고 찌르기는 끝까지 진행합니다.")]
    [SerializeField, Min(0f)] private float _thrustDistance = 8.9f;
    [Tooltip("Attack3 클립에서 찌르는 자세가 끝나는 지점입니다. 여기까지 돌진과 몸 판정을 하고, 이후(돌아오는 프레임)는 제자리에서 재생합니다. (애니메이션 진행도 0~1)\n현재 클립은 4프레임 중 앞 3프레임이 찌르는 자세이므로 0.75입니다.")]
    [SerializeField, Range(0f, 1f)] private float _thrustEndProgress = 0.75f;
    [Tooltip("찌르는 동안 플레이어가 몸에 닿았는지 확인하는 판정 범위입니다. 찌르는 모습(Attack3 클립 앞 3프레임의 불투명 영역)에 맞춘 값입니다.\nSoulKnight 위치 기준이며 오프셋 x는 찌르는 방향 기준입니다. 한 번의 찌르기에서 한 번만 맞습니다.")]
    [SerializeField] private HitboxShape _thrustBodyHitbox = new HitboxShape(new Vector2(0.94f, -0.05f), new Vector2(3f, 2.4f));
    [Tooltip("켜면 SoulKnight를 선택했을 때 Scene 뷰(프리팹 편집 화면 포함)에 찌르기 몸 판정 범위를 붉은 박스로 보여 줍니다. 끄면 이 공격의 박스만 숨깁니다.\n에디터 표시만 바꾸며 게임 동작에는 영향이 없습니다.")]
    [SerializeField] private bool _showThrustHitbox = true;
    [Tooltip("찌르기에 닿은 플레이어가 입는 피해량 배율입니다. 실제 피해 = SoulKnight 공격력 × 이 값입니다. 1 = 공격력 그대로")]
    [SerializeField, Min(0f)] private float _thrustDamageMultiplier = 1f;

    [Header("공격 4타 (내려치기)")]
    [Tooltip("내려치기의 판정 범위·피해 배율입니다. 판정 범위는 Attack4 클립에서 칼이 가장 멀리 나간 프레임에 맞춘 임시값입니다.\n내려치기는 전진하지 않으므로 전진 구간 값은 쓰지 않습니다.")]
    [SerializeField] private SoulKnightSwing _smashSwing = new SoulKnightSwing(new HitboxShape(new Vector2(1.75f, 0.03f), new Vector2(3.5f, 2.55f)), 1f, 0f, 0f);

    [Header("슬라이딩 (구르기 뒤)")]
    [Tooltip("슬라이딩으로 바닥을 미끄러지는 가로 속도입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _slideSpeed = 20f;
    [Tooltip("슬라이딩 중 몸에 닿은 플레이어가 입는 피해량 배율입니다. 실제 피해 = SoulKnight 공격력 × 이 값입니다. 1 = 공격력 그대로")]
    [SerializeField, Min(0f)] private float _slideDamageMultiplier = 1f;
    [Tooltip("슬라이딩 중 플레이어가 몸에 닿았는지 확인하는 판정 범위입니다. 누워서 미끄러지는 모습(Sliding 클립의 불투명 영역)에 맞춘 가로로 긴 직사각형입니다.\nSoulKnight 위치 기준이며 오프셋 x는 미끄러지는 방향 기준입니다. 한 번의 슬라이딩에서 한 번만 맞습니다.")]
    [SerializeField] private HitboxShape _slideBodyHitbox = new HitboxShape(new Vector2(0.7f, -0.65f), new Vector2(2.2f, 1.2f));
    [Tooltip("켜면 SoulKnight를 선택했을 때 Scene 뷰(프리팹 편집 화면 포함)에 슬라이딩 몸 충돌 범위를 붉은 박스로 보여 줍니다. 끄면 이 공격의 박스만 숨깁니다.\n에디터 표시만 바꾸며 게임 동작에는 영향이 없습니다.")]
    [SerializeField] private bool _showSlideHitbox = true;
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
    [Tooltip("켜면 SoulKnight를 선택했을 때 Scene 뷰(프리팹 편집 화면 포함)에 점프공격 충격 범위를 붉은 박스로 보여 줍니다. 끄면 이 공격의 박스만 숨깁니다.\n에디터 표시만 바꾸며 게임 동작에는 영향이 없습니다.")]
    [SerializeField] private bool _showJumpAttackHitbox = true;
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
    [Tooltip("슬라이딩 몸 충돌, 점프공격 충격, 공격 4타(1타·2타·찌르기·내려치기)가 플레이어에게 피해를 입힐 때마다 게이지가 줄어드는 양입니다. (최대값 100 기준이면 퍼센트)\nKnight와 같은 값입니다.")]
    [SerializeField, Min(0f)] private float _hitGaugeReduction = 20f;

    [Header("회복 (2페이즈)")]
    [Tooltip("회복(Health) 한 번에 회복하는 체력입니다. 최대 체력에 대한 비율이며, 회복 애니메이션이 끝나는 순간 회복합니다. (0.05 = 최대 체력의 5%)\n회복 모션 중에 피해를 입으면 맞는 순간 모션이 끊기고 바로 Idle로 가며, 체력은 오르지 않습니다.")]
    [SerializeField, Range(0f, 1f)] private float _healHealthRatio = 0.05f;

    // 2페이즈에서는 게이지 규칙이 바뀐다. 공격으로 차거나 적중으로 줄지 않고 시간에 따라서만 차며, 꽉 차면 기절 대신 기도로 이어진다.
    // 기도하는 동안에는 차던 속도 그대로 게이지가 빠지고, 다 빠지면 기도가 끝난다.
    [Header("2페이즈 패턴 게이지 (시간 충전)")]
    [Tooltip("2페이즈에서 게이지가 1초마다 차는 양입니다. 한 번에 차지 않고 이 속도로 계속 조금씩 찹니다. (최대값 100 기준이면 1초당 퍼센트, 5면 20초에 꽉 참)\n꽉 차면 하던 패턴을 마치고 Idle이 된 순간 기도를 시작합니다. 기도하는 동안에는 같은 속도로 게이지가 빠지고(5면 20초), 0이 되면 기도를 끝내고 Idle로 돌아갑니다. 플레이어가 죽어 있는 동안에는 차지 않습니다.")]
    [SerializeField, Min(0f)] private float _phase2GaugeFillPerSecond = 5f;
    [Tooltip("2페이즈 기도에서 게이지가 빠지는 동안 멈춰 두는 Praying 애니메이션의 지점입니다. (0~1, 클립 전체가 1) 이 지점 직전의 기도 자세를 유지하다가, 게이지가 다 빠지기 직전에 나머지(일어나는 모습)를 재생합니다.\n현재 클립(5초)은 4.5초부터 일어나므로 0.9입니다. 클립을 고치면 함께 맞춥니다. 1페이즈 기도에는 쓰지 않습니다.")]
    [SerializeField, Range(0f, 1f)] private float _phase2PrayingHoldProgress = 0.9f;

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

    [Header("1페이즈 다운 (체력 소진 시 페이즈 전환 연출)")]
    [Tooltip("1페이즈의 체력을 모두 잃었을 때 쓰러져 있는 시간입니다. 이 동안 시네마틱(카메라 줌인, 위아래 검은 띠, 전투 UI 숨김, 플레이어 입력 잠금)이 진행됩니다. (단위: 초)\n시간이 지나면 화면이 원래대로 돌아오고 체력이 충전된 뒤 2페이즈가 시작됩니다. 쓰러진 애니메이션은 반복 재생됩니다.")]
    [SerializeField, Min(0f)] private float _phase1DownDuration = 5f;

    [Header("출혈 연출 (1페이즈 다운부터)")]
    [Tooltip("1페이즈 다운(쓰러짐)에 들어갈 때 재생하는 Feel 피드백입니다. 몸의 출혈 효과(Visuals/MirroredVFX/Bleed/Bleeding)를 켜고 0.2초 동안 서서히 나타나게 합니다. 초기화 방식은 Script로 둡니다.\n출혈은 한 번 시작하면 2페이즈 내내, 사망 상태에서도 계속 흐릅니다. 비워 두면 출혈 없이 진행합니다.")]
    [SerializeField] private MMF_Player _bleedingFeedbacks;

    [Header("기도 연출")]
    [Tooltip("기도를 시작할 때 재생하는 Feel 피드백입니다. 발밑 룬(PrayingRune)을 켜고 0.2초 동안 서서히 나타나게 합니다. 초기화 방식은 Script로 둡니다.\n비워 두면 룬 없이 기도합니다.")]
    [SerializeField] private MMF_Player _prayingRuneFeedbacks;
    [Tooltip("기도가 끝날 때 재생하는 Feel 피드백입니다. 발밑 룬을 0.2초 동안 서서히 지운 뒤 끕니다. 초기화 방식은 Script로 둡니다.")]
    [SerializeField] private MMF_Player _prayingRuneEndFeedbacks;

    [Header("각성 연출 (1페이즈 기도 끝 ~ 1페이즈 다운)")]
    [Tooltip("1페이즈 기도가 끝날 때 재생하는 Feel 피드백입니다. 몸 주위의 보라색 오라(Visuals/MirroredVFX/Awaken/AwakenAura)를 켜고, 크게 터져 나왔다가 몸 가까이로 줄어들게 합니다. 초기화 방식은 Script로 둡니다.\n오라는 1페이즈가 끝날 때(1페이즈 다운)까지 계속 보입니다. 비워 두면 오라 없이 진행합니다.")]
    [SerializeField] private MMF_Player _awakenAuraFeedbacks;
    [Tooltip("1페이즈 다운(쓰러짐)에 들어갈 때 재생하는 Feel 피드백입니다. 오라를 0.2초 동안 서서히 지운 뒤 끕니다. 초기화 방식은 Script로 둡니다.")]
    [SerializeField] private MMF_Player _awakenAuraEndFeedbacks;
    [Tooltip("1페이즈 기도 애니메이션이 이 지점에 도달하면 오라를 시작합니다. (0~1, 클립 전체가 1)\n현재 클립(5초)은 4.75초에 완전히 일어선 모습(Pray_11)이 되므로 0.95입니다. 클립을 고치면 함께 맞춥니다.")]
    [SerializeField, Range(0f, 1f)] private float _awakenAuraStartProgress = 0.95f;

    [Header("기도 (체력 50% 이하)")]
    [Tooltip("각성 전 Idle 중 현재 체력이 최대 체력의 이 비율 이하이면 각성하고 기도를 시작합니다. 각성은 전투당 한 번이라 기도도 한 번만 합니다.\n각성 전에는 체력이 1 아래로 내려가지 않으므로 큰 피해로 기도 전에 죽지 않습니다.\n0.5 = 체력 50% 이하")]
    [SerializeField, Range(0f, 1f)] private float _prayingHealthRatio = 0.5f;

    [Header("모션 테스트")]
    [Tooltip("켜면 전투 패턴 대신 Idle 사이에 모든 모션을 순서대로 시험합니다. 공격 4타와 점프·점프공격은 이어서 재생하며 사망은 모션만 미리 봅니다.\n끄면 실제 전투 패턴을 시전합니다.")]
    [SerializeField] private bool _autoPreview;
    private int _previewIndex;
    private SoulKnightAnimation _soulKnightAnimation;
    private bool _isStunReadyEffectActive;
    // 피격음 재생기. 2페이즈에서 게이지가 꽉 찬 동안에도 피격음이 나도록 규칙을 바꿀 때 쓴다. 없으면 null이다.
    private HitSoundPlayer _hitSoundPlayer;
    // 출혈 효과를 이미 시작했는지. 전투당 한 번만 시작한다.
    private bool _isBleeding;
    private bool _isAwakenAuraActive;
    // 2페이즈 기도 기믹(기둥). 루트에 SoulKnightPillarGimmick이 없으면 null이며 기둥 없이 기도한다.
    private SoulKnightPillarGimmick _pillarGimmick;
    // Ghost 레이어로 바꿔 둔 상태인지와, 되돌릴 원래 레이어 (루트 몸 콜라이더, 피격 판정 콜라이더)
    private bool _isGhost;
    private int _ghostLayer;
    private int _bodyLayer;
    private int _hurtboxLayer;
    // 이 보스가 연 시네마틱이 진행 중인지
    private bool _isCinematicPlaying;

    public bool AutoPreview => _autoPreview;
    // 1페이즈 각성 여부. 체력이 기도 비율(50%) 이하인 것을 Idle에서 처음 확인해 기도에 들어가는 순간 true가 된다. 전투가 시작되면 false다.
    // 각성 전에는 피해로 체력이 1 아래로 내려가지 않고(PreAwakeningMinHealth), 각성한 뒤에는 제한이 없어진다.
    // 기도는 각성할 때 한 번만 하므로, 이후 회복 등으로 체력이 다시 50% 위로 올랐다 내려와도 다시 기도하지 않는다.
    public bool IsAwakened { get; private set; }
    // 2페이즈부터는 패턴 게이지가 시간으로만 찬다. (공격으로 차거나 적중으로 줄지 않고, 꽉 차면 기절 대신 기도)
    private bool UsesTimedGauge => CurrentPhase >= 2;

    public SoulKnightRollState RollState { get; private set; }
    public SoulKnightSlideState SlideState { get; private set; }
    public SoulKnightJumpState JumpState { get; private set; }
    public SoulKnightJumpAttackState JumpAttackState { get; private set; }
    public SoulKnightStunState StunState { get; private set; }
    public SoulKnightStunRecoverState StunRecoverState { get; private set; }
    public SoulKnightPrayingState PrayingState { get; private set; }
    public SoulKnightPhase1DownState Phase1DownState { get; private set; }
    public SoulKnightAttackDelayState AttackDelayState { get; private set; }
    public SoulKnightAttack1State Attack1State { get; private set; }
    public SoulKnightAttack2State Attack2State { get; private set; }
    public SoulKnightThrustState ThrustState { get; private set; }
    public SoulKnightSmashState SmashState { get; private set; }
    public SoulKnightHealthState HealthState { get; private set; }

    public float Phase1DownDuration => _phase1DownDuration;
    public float AttackStepSpeed => _attackStepSpeed;
    public float AttackGaugeGain => _attackGaugeGain;
    public SoulKnightSwing Attack1Swing => _attack1Swing;
    public SoulKnightSwing Attack2Swing => _attack2Swing;
    public SoulKnightSwing SmashSwing => _smashSwing;
    public float ThrustDistance => _thrustDistance;
    public float ThrustEndProgress => _thrustEndProgress;
    public HitboxShape ThrustBodyHitbox => _thrustBodyHitbox;
    public float ThrustDamage => Stats.AttackPower * _thrustDamageMultiplier;

    public float PatternGaugeGain => _patternGaugeGain;
    public float Phase2GaugeFillPerSecond => _phase2GaugeFillPerSecond;
    // 회복 한 번에 오르는 체력
    public float HealAmount => Vital.MaxHealth * _healHealthRatio;
    public float Phase2PrayingHoldProgress => _phase2PrayingHoldProgress;
    public float AwakenAuraStartProgress => _awakenAuraStartProgress;
    // 2페이즈 기도 기믹에서 플레이어가 기둥 위 목표물을 공격했는지
    public bool IsPrayingGimmickCleared => _pillarGimmick != null && _pillarGimmick.IsCleared;
    private bool IsPrayingGimmickBusy => _pillarGimmick != null && _pillarGimmick.IsBusy;
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
        _hitSoundPlayer = TryGetComponent(out HitSoundPlayer hitSoundPlayer) ? hitSoundPlayer : null;

        if (_hitSoundPlayer != null)
        {
            _hitSoundPlayer.SetGaugeMuteEnabled(true);
        }
        _isBleeding = false;
        _isAwakenAuraActive = false;
        _pillarGimmick = TryGetComponent(out SoulKnightPillarGimmick pillarGimmick) ? pillarGimmick : null;
        _isGhost = false;
        _ghostLayer = LayerMask.NameToLayer(GhostLayerName);
        _bodyLayer = gameObject.layer;
        _hurtboxLayer = Hurtbox != null ? Hurtbox.gameObject.layer : gameObject.layer;

        if (_ghostLayer < 0)
        {
            Debug.LogError($"SoulKnight: '{GhostLayerName}' 레이어가 없습니다. 2페이즈 기도 중 기둥에 밀리거나 공격을 맞을 수 있습니다.", this);
        }

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

        // 기도 룬 피드백은 초기화할 때 룬을 꺼 둔다.
        if (_prayingRuneFeedbacks != null)
        {
            _prayingRuneFeedbacks.Initialization();
        }

        if (_prayingRuneEndFeedbacks != null)
        {
            _prayingRuneEndFeedbacks.Initialization();
        }

        // 출혈 피드백도 초기화할 때 출혈 효과를 꺼 둔다.
        if (_bleedingFeedbacks != null)
        {
            _bleedingFeedbacks.Initialization();
        }

        // 각성 오라 피드백도 초기화할 때 오라를 꺼 둔다.
        if (_awakenAuraFeedbacks != null)
        {
            _awakenAuraFeedbacks.Initialization();
        }

        if (_awakenAuraEndFeedbacks != null)
        {
            _awakenAuraEndFeedbacks.Initialization();
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
        Phase1DownState = new SoulKnightPhase1DownState(this, stateMachine);
        AttackDelayState = new SoulKnightAttackDelayState(this, stateMachine);
        Attack1State = new SoulKnightAttack1State(this, stateMachine);
        Attack2State = new SoulKnightAttack2State(this, stateMachine);
        ThrustState = new SoulKnightThrustState(this, stateMachine);
        SmashState = new SoulKnightSmashState(this, stateMachine);
        HealthState = new SoulKnightHealthState(this, stateMachine);

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

        // 2페이즈 기도 기믹의 기둥이 다 내려갈 때까지는 Idle로 기다린다. (벽을 찾아 움직이는 패턴이 기둥을 벽으로 잘못 보지 않게 한다)
        if (_isGhost)
        {
            return;
        }

        if (_autoPreview)
        {
            PlayMotion(_previewSequence[_previewIndex]);
            _previewIndex = (_previewIndex + 1) % _previewSequence.Length;
            return;
        }

        if (IsAwakened)
        {
            ChangeToAwakenedPattern();
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

    // 각성 후: 구르기→슬라이딩, 점프→점프공격, 공격 4타 중 하나를 가중치 비율로 고른다. 가중치가 모두 0이면 공격 4타를 고른다.
    // 2페이즈에서는 여기에 회복(Health)이 더해져 넷 중 하나를 고른다. (가중치가 같으면 각 25%)
    // 체력이 가득 차 있으면 회복할 것이 없으므로 회복을 빼고 나머지 셋 중에서 고른다.
    private void ChangeToAwakenedPattern()
    {
        bool canHeal = CurrentPhase >= 2 && Vital.CurrentHealth < Vital.MaxHealth;
        float healthWeight = canHeal ? _phase2HealthWeight : 0f;
        float totalWeight = _awakenedRollWeight + _awakenedJumpWeight + _awakenedAttackWeight + healthWeight;

        if (totalWeight <= 0f)
        {
            ChangeToAttackPattern();
            return;
        }

        float pick = Random.value * totalWeight;

        if (pick < _awakenedRollWeight)
        {
            stateMachine.ChangeState(RollState);
        }
        else if (pick < _awakenedRollWeight + _awakenedJumpWeight)
        {
            stateMachine.ChangeState(JumpState);
        }
        else if (pick < _awakenedRollWeight + _awakenedJumpWeight + healthWeight)
        {
            stateMachine.ChangeState(HealthState);
        }
        else
        {
            ChangeToAttackPattern();
        }
    }

    // 공격 4타를 시작한다. 먼저 딜레이(AttackDelayState)로 잠깐 멈춘 뒤, 플레이어가 사거리 안이면 1타부터, 밖이면 찌르기(3타)부터 시작해 내려치기까지 잇는다.
    // 첫 공격은 딜레이에 들어가는 시점의 거리로 정한다.
    private void ChangeToAttackPattern()
    {
        AttackDelayState.SetNextState(DistanceToTarget <= _attackRange ? Attack1State : ThrustState);
        stateMachine.ChangeState(AttackDelayState);
    }

    protected override void OnRelease()
    {
        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.StopFeedbacks();
        }

        StopPrayingRuneFeedbacks();
        if (_bleedingFeedbacks != null)
        {
            _bleedingFeedbacks.StopFeedbacks();
        }

        // 2페이즈 기도 기믹 도중에 Release되면 기둥을 바로 치우고 레이어를 되돌린다.
        if (_pillarGimmick != null)
        {
            _pillarGimmick.Clear();
        }

        SetGhost(false);

        if (_awakenAuraFeedbacks != null)
        {
            _awakenAuraFeedbacks.StopFeedbacks();
        }

        if (_awakenAuraEndFeedbacks != null)
        {
            _awakenAuraEndFeedbacks.StopFeedbacks();
        }

        // 시네마틱 도중에 Release되면 상태의 Exit이 호출되지 않으므로 여기서 닫는다. (플레이어 입력과 카메라가 잠긴 채 남지 않게 한다)
        EndCinematic();
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
    // 2페이즈에서는 체력과 관계없이, 시간으로 찬 패턴 게이지가 꽉 차 있으면 기도를 시작한다. 몇 번이든 반복한다.
    // 게이지는 여기서 비우지 않는다. 기도 상태가 차던 속도로 조금씩 빼고, 다 빠지면 기도를 끝낸다.
    public override bool TryInterruptIdle()
    {
        if (_autoPreview || Vital.IsDead)
        {
            return false;
        }

        if (UsesTimedGauge)
        {
            if (!PatternGauge.IsFull)
            {
                return false;
            }

            stateMachine.ChangeState(PrayingState);
            return true;
        }

        if (IsAwakened || Vital.CurrentHealth > Vital.MaxHealth * _prayingHealthRatio)
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

    // 출혈을 시작한다. 몸의 상처 3곳에서 피가 흐르는 효과를 0.2초 동안 서서히 나타나게 한다. (1페이즈 다운에 들어갈 때 호출)
    // 한 번 시작하면 2페이즈 내내, 사망 상태에서도 끄지 않는다. 이미 나와 있으면 다시 시작하지 않는다.
    public void PlayBleeding()
    {
        if (_bleedingFeedbacks == null || _isBleeding)
        {
            return;
        }

        _isBleeding = true;
        _bleedingFeedbacks.PlayFeedbacks();
    }

    // 새 페이즈가 시작될 때: 2페이즈부터는 게이지가 꽉 차도 기절하지 않으므로, 꽉 찬 동안 피격음을 생략하던 규칙과
    // 게이지 바의 꽉 참 강조(튕김·깜빡임)를 끈다. 둘 다 "지금 때리면 기절"을 알리는 표시라 2페이즈에는 맞지 않는다.
    protected override void OnPhaseStarted(int phase)
    {
        if (phase < 2)
        {
            return;
        }

        PatternGauge.SetFullEmphasisEnabled(false);

        if (_hitSoundPlayer != null)
        {
            _hitSoundPlayer.SetGaugeMuteEnabled(false);
        }
    }

    // 2페이즈의 게이지 시간 충전. 매 프레임 조금씩 채운다. (한 번에 차지 않는다)
    // 기도 중에는 차지 않고(기도 상태가 같은 속도로 뺀다), 플레이어가 죽어 있는 동안에도 차지 않는다. 사망했거나 페이즈 전환 중일 때도 마찬가지다.
    // Enemy의 Update(상태기계 구동) 뒤에 실행되도록 LateUpdate를 쓴다. Enemy에 LateUpdate가 생기면 이 함수가 그것을 가리게 되므로 그때는 합쳐야 한다.
    private void LateUpdate()
    {
        if (!IsInitialized)
        {
            return;
        }

        // 2페이즈 기도가 끝나고 기둥이 다 내려갔으면 Ghost 레이어를 원래 레이어로 되돌린다.
        if (_isGhost && CurrentState != PrayingState && !IsPrayingGimmickBusy)
        {
            SetGhost(false);
        }

        if (!UsesTimedGauge || Vital.IsDead || IsPhaseTransitioning || IsTargetDead || CurrentState == PrayingState)
        {
            return;
        }

        PatternGauge.Add(_phase2GaugeFillPerSecond * Time.deltaTime);
    }

    // 1페이즈의 체력을 모두 잃었을 때: 진행 중인 패턴을 끊고 1페이즈 다운 연출로 들어간다. 연출이 끝나면 그 상태가 StartPhaseRefill을 호출한다.
    protected override void OnPhaseHealthDepleted()
    {
        stateMachine.ChangeState(Phase1DownState);
    }

    // 시네마틱을 연다. 카메라(CinematicManager), 검은 띠·전투 UI(CinematicUI), 플레이어 입력(Player)이 이벤트를 받아 처리한다.
    // 이미 열려 있으면 다시 열지 않는다. 연 쪽은 EndCinematic으로 반드시 닫는다.
    public void BeginCinematic()
    {
        if (_isCinematicPlaying)
        {
            return;
        }

        _isCinematicPlaying = true;
        EventBus<CinematicStartedEvent>.Publish(new CinematicStartedEvent(gameObject));
    }

    // 시네마틱을 닫는다. 열려 있지 않으면 아무것도 하지 않는다.
    public void EndCinematic()
    {
        if (!_isCinematicPlaying)
        {
            return;
        }

        _isCinematicPlaying = false;
        EventBus<CinematicEndedEvent>.Publish(new CinematicEndedEvent(gameObject));
    }

    // 기도를 시작할 때 발밑 룬을 서서히 나타나게 한다. 사라지는 중이었다면 멈추고 다시 나타낸다.
    public void PlayPrayingRune()
    {
        if (_prayingRuneEndFeedbacks != null)
        {
            _prayingRuneEndFeedbacks.StopFeedbacks();
        }

        if (_prayingRuneFeedbacks != null)
        {
            _prayingRuneFeedbacks.PlayFeedbacks();
        }
    }

    // 기도가 끝날 때 발밑 룬을 서서히 지운 뒤 끈다.
    public void StopPrayingRune()
    {
        if (_prayingRuneFeedbacks != null)
        {
            _prayingRuneFeedbacks.StopFeedbacks();
        }

        if (_prayingRuneEndFeedbacks != null)
        {
            _prayingRuneEndFeedbacks.PlayFeedbacks();
        }
    }

    // 2페이즈 기도 기믹을 시작한다. 보스를 Ghost 레이어로 바꾸고 기둥을 솟아오르게 한다. (2페이즈 기도에 들어갈 때 호출)
    public void BeginPrayingGimmick()
    {
        SetGhost(true);

        if (_pillarGimmick != null)
        {
            _pillarGimmick.Begin(Movement.Position, Target != null ? Target.BodyCollider : null);
        }
    }

    // 2페이즈 기도 기믹을 끝낸다. 기둥을 내려보낸다. (2페이즈 기도에서 나갈 때 호출)
    // Ghost 레이어는 여기서 되돌리지 않고, 기둥이 다 내려간 뒤 LateUpdate에서 되돌린다. (내려가는 기둥에 몸이 밀리지 않게 한다)
    public void EndPrayingGimmick()
    {
        if (_pillarGimmick != null)
        {
            _pillarGimmick.End();
        }
    }

    // 보스의 몸 콜라이더(루트)와 피격 판정 콜라이더(자식 Collider)를 Ghost 레이어로 바꾸거나 원래 레이어로 되돌린다.
    private void SetGhost(bool isGhost)
    {
        if (_isGhost == isGhost || _ghostLayer < 0)
        {
            return;
        }

        _isGhost = isGhost;
        gameObject.layer = isGhost ? _ghostLayer : _bodyLayer;

        if (Hurtbox != null)
        {
            Hurtbox.gameObject.layer = isGhost ? _ghostLayer : _hurtboxLayer;
        }
    }

    // 각성 오라를 시작한다. 1페이즈 기도가 끝날 때(일어서는 순간) 몸 주위로 보라색 기운이 터져 나왔다가 일렁이는 오라로 남는다.
    // 1페이즈 다운까지 계속 보이며, 이미 나와 있으면 다시 시작하지 않는다.
    public void PlayAwakenAura()
    {
        if (_awakenAuraFeedbacks == null || _isAwakenAuraActive)
        {
            return;
        }

        _isAwakenAuraActive = true;

        if (_awakenAuraEndFeedbacks != null)
        {
            _awakenAuraEndFeedbacks.StopFeedbacks();
        }

        _awakenAuraFeedbacks.PlayFeedbacks();
    }

    // 각성 오라를 서서히 지운 뒤 끈다. (1페이즈 다운에 들어갈 때 호출) 나와 있지 않으면 아무것도 하지 않는다.
    public void StopAwakenAura()
    {
        if (!_isAwakenAuraActive)
        {
            return;
        }

        _isAwakenAuraActive = false;
        _awakenAuraFeedbacks.StopFeedbacks();

        if (_awakenAuraEndFeedbacks != null)
        {
            _awakenAuraEndFeedbacks.PlayFeedbacks();
        }
    }

    private void StopPrayingRuneFeedbacks()
    {
        if (_prayingRuneFeedbacks != null)
        {
            _prayingRuneFeedbacks.StopFeedbacks();
        }

        if (_prayingRuneEndFeedbacks != null)
        {
            _prayingRuneEndFeedbacks.StopFeedbacks();
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

    // 공격(슬라이딩, 점프공격, 공격 4타)을 시작할 때 게이지를 채운다. 2페이즈에서는 시간으로만 차므로 채우지 않는다.
    public void AddPatternGaugeByAttack(float amount)
    {
        if (UsesTimedGauge)
        {
            return;
        }

        PatternGauge.Add(amount);
    }

    // 슬라이딩 몸 충돌, 점프공격 충격, 공격 4타가 플레이어를 맞혔을 때 호출한다. 맞힌 수만큼 게이지를 줄인다. 2페이즈에서는 줄이지 않는다.
    public void ReducePatternGaugeByHits(int hitCount)
    {
        if (hitCount <= 0 || UsesTimedGauge)
        {
            return;
        }

        PatternGauge.Reduce(_hitGaugeReduction * hitCount);
    }

    // 실제로 다음 피격에 기절할 수 있는 동안만 표시한다. 기절 진입 시에는 게이지가 아직 꽉 차 있어도 끈다. 2페이즈에는 기절이 없으므로 표시하지 않는다.
    public void RefreshStunReadyEffect()
    {
        bool isReady = IsInitialized && !UsesTimedGauge && !Vital.IsDead && !IsTargetDead && PatternGauge.IsFull
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
        // 회복 모션 중에 피해를 입으면 회복을 끊는다. 맞는 순간 모션을 멈추고 바로 Idle로 쉬어가며 체력은 오르지 않는다.
        // 치명 피해라면 바로 뒤에 이어지는 사망 처리에 맡긴다.
        if (CurrentState == HealthState && !Vital.IsDead)
        {
            ChangeToRest();
        }

        // 치명 피해는 사망을 우선하며, 기절 중 추가 피격으로 기절 시간을 초기화하지 않는다. 2페이즈에는 기절이 없다. (게이지가 꽉 차면 기도로 이어진다)
        if (UsesTimedGauge || Vital.IsDead || !PatternGauge.IsFull || CurrentState == StunState || CurrentState == StunRecoverState)
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

    // 선택했을 때 슬라이딩 몸 충돌, 점프공격 충격, 공격 4타의 판정 범위를 그린다. 공격마다 인스펙터의 판정 범위 표시를 끄면 그 공격만 그리지 않는다.
    protected override void DrawHitboxGizmos(float facingDirection)
    {
        if (_showSlideHitbox)
        {
            DrawHitboxGizmo(_slideBodyHitbox, facingDirection, "슬라이딩 몸 충돌");
        }

        if (_showJumpAttackHitbox)
        {
            DrawHitboxGizmo(_jumpAttackImpactHitbox, facingDirection, "점프공격 충격");
        }

        if (_attack1Swing.ShowHitbox)
        {
            DrawHitboxGizmo(_attack1Swing.Hitbox, facingDirection, "공격 1타");
        }

        if (_attack2Swing.ShowHitbox)
        {
            DrawHitboxGizmo(_attack2Swing.Hitbox, facingDirection, "공격 2타");
        }

        if (_showThrustHitbox)
        {
            DrawHitboxGizmo(_thrustBodyHitbox, facingDirection, "찌르기 몸 충돌");
        }

        if (_smashSwing.ShowHitbox)
        {
            DrawHitboxGizmo(_smashSwing.Hitbox, facingDirection, "내려치기");
        }
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

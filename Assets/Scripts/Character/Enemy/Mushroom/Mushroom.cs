using MoreMountains.Feedbacks;
using UnityEngine;

// Mushroom(머쉬룸) 보스. 1페이즈만 있는 지상형 보스다.
// 흐름: Idle → (무작위) 점프, 또는 플레이어가 땅이면 런 / 공중이면 발사
//  - 런(Run): 제자리에서 뛰는 준비 동작 뒤, 준비 도중에 기억해 둔 플레이어의 가로 위치까지 바닥을 따라 아주 빠르게 달린다.
//    끝나면 Idle, 또는 플레이어가 공중이면 발사 / 땅이면 점프.
//  - 발사(LaunchReady → Launch): 준비 동작 뒤, 준비 도중에 기억해 둔 플레이어 위치(공중 포함)까지 일직선으로 날아간다. 끝나면 점프(뛸 수 있을 때만) 또는 점프공격.
//  - 점프(Jump): 제자리에서 위로 뛰어오른다. 처음에는 빠르고 갈수록 느려진다. 끝나면 점프공격, 또는 공중점프가 남아 있으면 한 번 더 점프한다.
//    공중에서 시작한 점프는 공중점프다. (높이·시간이 다르고 공중점프 횟수를 쓴다) 공중점프 횟수는 땅에 닿으면 다시 채운다.
//  - 점프공격(JumpAttack): 그 자리에 멈춘 준비 동작 뒤, 준비 도중에 기억해 둔 플레이어 쪽으로 벽·땅·천장에 닿을 때까지 계속 돌진한다. 끝나면 Idle.
// 런·발사·점프공격으로 움직이는 동안 몸에 닿은 플레이어가 피해를 입는다. (한 번의 패턴에서 한 번만)
// 패턴이 공중에서 끝나 Idle로 가야 하면 먼저 떨어져서(Fall) 착지한 뒤 Idle로 쉰다.
// 패턴 게이지: Gauge Fill Duration(25초)에 걸쳐 저절로 차고, 몸 판정(점프공격 충격파 포함)으로 플레이어를 맞힐 때마다 Hit Gauge Gain Ratio(10%)만큼 더 찬다.
//  꽉 차면(게이지 UI가 푸르게 깜빡인다) 하던 패턴을 마치고 Idle에 들어가는 순간 반응속도 기믹(MushroomReactionState)을 시작한다.
//  - 반응속도 기믹: 시네마틱(검은 띠·줌인)으로 둘을 화면 양쪽에 세우고, 준비자세로 2~10초 중 무작위 시간 뒤 플레이어에게 날아간다.
//    닿기 전에 플레이어가 먼저 때리면 스턴(Stun)에 걸리고, 못 때리면 공격력의 3배 피해를 입힌다.
//  - 스턴(Stun): Stun Duration(5초) 동안 그 자리에 멈춰 있다가 Idle로 돌아간다. 기믹에서만 걸린다.
[RequireComponent(typeof(PatternGauge))]
public class Mushroom : Enemy
{
    private const string GroundLayerName = "Ground";
    private const string WallLayerName = "Wall";
    // 이동 중 지형을 찾을 때 한 번에 처리할 최대 결과 수
    private const int MaxCastCount = 4;
    // 이동하다 지형에 닿을 때 콜라이더 사이에 남기는 간격 (단위: 월드 거리)
    private const float CastContactSkin = 0.02f;
    // 지형을 찾는 박스를 몸 콜라이더보다 사방으로 이만큼 줄인다. 발밑 바닥처럼 이미 맞닿아 있는 지형이 겹친 것으로 잡히지 않게 한다. (단위: 월드 거리)
    private const float CastShrink = 0.02f;
    // 닿은 면이 움직이는 방향을 이 값보다 더 마주 볼 때만 막힌 것으로 본다. (면의 법선과 이동 방향의 내적, 0이면 스치는 면)
    // 바닥을 따라 가로로 움직일 때의 발밑 바닥(내적 0)만 걸러 내도록 0에 가깝게 둔다. 공중에서 완만하게 내려오다 바닥에 닿는 것은 막힌 것이다.
    private const float BlockingNormalDot = -0.001f;
    // 전투 시작 때 양쪽 벽을 찾는 최대 거리 (단위: 월드 거리)
    private const float WallSearchDistance = 100f;
    // 조준점이 벽에 붙을 때, 몸 콜라이더 가장자리와 벽 사이에 남기는 간격. 벽에 붙은 플레이어를 겨눠도 몸이 벽에 먼저 걸리지 않게 한다. (단위: 월드 거리)
    private const float AimWallGap = 0.05f;
    // 조준점과의 가로 차이가 이 값 이하면 그 차이로 보는 방향을 정하지 않는다. (단위: 월드 거리)
    private const float FacingDeadZone = 0.1f;
    // 임팩트 프레임이 화면 가장자리 밖으로 더 덮고 있어야 하는 여유. 피격 카메라 흔들림(가로 최대 0.9)으로 화면이 움직여도 가장자리가 비지 않게 한다. (단위: 월드 거리)
    private const float ImpactFrameCoverMargin = 1f;

    [Header("패턴")]
    [Tooltip("패턴이 끝나고 Idle로 쉬는 시간입니다. 전투 시작 때의 Idle 시간은 '시작' 항목의 Start Idle Duration에서 따로 정합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _patternInterval = 2f;
    [Tooltip("Idle이 끝났을 때 점프를 고를 확률입니다. 0이면 항상 런/발사, 1이면 항상 점프입니다. 점프를 고르지 않으면 그 순간 플레이어가 땅에 있으면 런, 공중에 있으면 발사입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _idleJumpChance = 0.5f;

    [Header("몸 충돌 (런·발사·점프공격 공통)")]
    [Tooltip("런으로 달리거나 발사·점프공격으로 날아가는 동안의 몸 충돌 판정 범위입니다. 이 범위에 닿은 플레이어가 피해를 입습니다. 머쉬룸 그림 크기에 맞춘 값입니다.")]
    [SerializeField] private HitboxShape _bodyHitbox = new HitboxShape(Vector2.zero, new Vector2(1.9f, 2.06f));

    [Header("속도선 (런·발사·점프공격 공통)")]
    [Tooltip("빠르게 움직이는 동안 몸 뒤로 지나가는 속도선 효과(pixelFX status/haste-lines, 흰 공기 색)의 회전 기준입니다. Visuals/MirroredVFX/Speed/SpeedLines를 연결합니다. 켤 때 움직이는 방향으로 기울입니다. 켜고 끄는 것은 아래 두 피드백이 맡습니다.\n비워 두면 기울이지 않습니다.")]
    [SerializeField] private Transform _speedLines;
    [Tooltip("런으로 달리기 시작할 때, 발사로 날아가기 시작할 때, 점프공격으로 돌진하기 시작할 때 속도선을 켜는 피드백입니다. Visuals/MirroredVFX/Speed/SpeedLines_Enter의 MMF_Player를 연결합니다.\n비워 두면 속도선이 나오지 않습니다.")]
    [SerializeField] private MMF_Player _speedLinesEnter;
    [Tooltip("움직임이 끝나거나(런·발사가 끝날 때, 점프공격이 벽·땅에 닿을 때) 중간에 끊길 때 속도선을 끄는 피드백입니다. Visuals/MirroredVFX/Speed/SpeedLines_Exit의 MMF_Player를 연결합니다.")]
    [SerializeField] private MMF_Player _speedLinesExit;

    [Header("런")]
    [Tooltip("런의 준비 동작(제자리에서 뛰기) 시간입니다. 이 시간이 지나면 기억해 둔 플레이어의 가로 위치로 달립니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _runReadyDuration = 0.5f;
    [Tooltip("런의 준비 동작이 얼마나 진행됐을 때 플레이어 위치를 기억할지 정합니다. 0이면 준비를 시작하는 순간, 0.5면 준비 시간의 절반이 지났을 때, 1이면 준비가 끝나는 순간입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _runAimTimeRatio = 0.5f;
    [Tooltip("준비 동작이 끝난 뒤 달리는 속도입니다. 달리는 동안 일정합니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _runSpeed = 40f;
    [Tooltip("달리기가 목표 지점에 닿지 못하는 예외 상황에서 달리기를 끝내는 최대 시간입니다. (단위: 초)")]
    [SerializeField, Min(0.1f)] private float _runMaxDuration = 1f;
    [Tooltip("달리는 동안 몸에 닿은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 한 번의 런에서 한 번만 맞습니다. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _runDamageMultiplier = 1f;
    [Tooltip("런이 끝났을 때 Idle로 쉬지 않고 바로 다음 패턴을 시전할 확률입니다. 다음 패턴은 그 순간 플레이어가 공중에 있으면 발사, 땅에 있으면 점프입니다. 0이면 항상 Idle, 1이면 항상 다음 패턴이고, 나머지 경우는 Idle입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _runFollowUpChance = 0.5f;
    [Tooltip("런을 시작할 때(준비 동작부터) 발밑 먼지를 켜는 피드백입니다. Visuals/MirroredVFX/Run/RunDust_Enter의 MMF_Player를 연결합니다.\n비워 두면 먼지가 나오지 않습니다.")]
    [SerializeField] private MMF_Player _runDustEnter;
    [Tooltip("런이 끝나거나 중간에 끊길 때 발밑 먼지를 끄는 피드백입니다. Visuals/MirroredVFX/Run/RunDust_Exit의 MMF_Player를 연결합니다.")]
    [SerializeField] private MMF_Player _runDustExit;

    [Header("발사")]
    [Tooltip("발사의 준비 동작 시간입니다. 이 시간이 지나면 기억해 둔 플레이어 위치로 날아갑니다. 준비 그림(Launch 클립의 앞 4장)은 0.5초 길이에 맞춰져 있습니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _launchReadyDuration = 0.5f;
    [Tooltip("발사의 준비 동작이 얼마나 진행됐을 때 플레이어 위치를 기억할지 정합니다. 0이면 준비를 시작하는 순간, 0.5면 준비 시간의 절반이 지났을 때, 1이면 준비가 끝나는 순간입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _launchAimTimeRatio = 0.5f;
    [Tooltip("기억해 둔 플레이어 위치로 일직선으로 날아가는 속도입니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _launchSpeed = 30f;
    [Tooltip("발사가 목표 지점에 닿지 못하는 예외 상황에서 발사를 끝내는 최대 시간입니다. (단위: 초)")]
    [SerializeField, Min(0.1f)] private float _launchMaxDuration = 1f;
    [Tooltip("날아가는 동안 몸에 닿은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 한 번의 발사에서 한 번만 맞습니다. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _launchDamageMultiplier = 1f;
    [Tooltip("발사가 끝났을 때 점프를 고를 확률입니다. 나머지 경우는 점프공격입니다. 공중에서 끝났다면 공중점프로 뛰며, 공중점프가 남아 있지 않으면 항상 점프공격입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _launchJumpChance = 0.5f;

    [Header("점프")]
    [Tooltip("땅에서 뛰는 점프가 올라가는 높이입니다. 처음에는 카메라 화면 높이(10)의 절반(5)이었다가 너무 높아 4분의 3(3.75)으로 줄였습니다. 공중점프 높이는 이 값에 Air Jump Height Ratio를 곱합니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _jumpHeight = 3.75f;
    [Tooltip("땅에서 뛰는 점프의 준비 시간입니다. 이 동안 제자리에 멈춘 채 Jump 애니메이션(클립 0.4초)을 재생합니다. 끝나면 마지막 그림을 유지한 채 Jump Rise Duration 동안 솟아오릅니다. 클립 길이(0.4초)보다 짧게 하면 마지막 그림이 나오기 전에 솟아오르고, 0이면 바로 솟아오릅니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _jumpReadyDuration = 0.4f;
    [Tooltip("땅에서 뛰는 점프가 준비를 마친 뒤 Jump Height까지 솟아오르는 시간입니다. 점프 전체 시간은 Jump Ready Duration + 이 값(0.5초)입니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _jumpRiseDuration = 0.1f;
    [Tooltip("솟아오르기가 끝나는 순간의 속도가 시작 속도의 몇 배인지 정합니다. 1이면 일정한 속도, 작을수록 처음에 빠르고 끝에 느립니다. 속도는 시간에 비례해 줄어듭니다. 공중점프에도 같이 적용됩니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _jumpEndSpeedRatio = 1f / 3f;
    [Tooltip("땅에 닿기 전까지 쓸 수 있는 공중점프 횟수입니다. 땅에 닿으면 다시 채웁니다. 0이면 공중점프를 하지 않습니다.")]
    [SerializeField, Min(0)] private int _airJumpCount = 1;
    [Tooltip("공중점프가 올라가는 높이의 배율입니다. 높이 = Jump Height x 이 값. (0.5면 절반 높이)")]
    [SerializeField, Min(0f)] private float _airJumpHeightRatio = 0.5f;
    [Tooltip("공중점프의 준비 시간입니다. 이 동안 공중의 그 자리에 멈춘 채 AirJump 애니메이션(Jump 클립 2배속, 0.2초)을 재생합니다. 끝나면 마지막 그림을 유지한 채 Air Jump Rise Duration 동안 솟아오릅니다. 0.2초보다 짧게 하면 마지막 그림이 나오기 전에 솟아오르고, 0이면 바로 솟아오릅니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _airJumpReadyDuration = 0.2f;
    [Tooltip("공중점프가 준비를 마친 뒤 Jump Height x Air Jump Height Ratio까지 솟아오르는 시간입니다. 공중점프 전체 시간은 Air Jump Ready Duration + 이 값(0.25초)입니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _airJumpRiseDuration = 0.05f;
    [Tooltip("점프가 끝났을 때 공중점프가 남아 있으면 한 번 더 점프할 확률입니다. 0이면 항상 점프공격, 1이면 항상 공중점프이고, 나머지 경우는 점프공격입니다. 공중점프가 남아 있지 않으면 항상 점프공격입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _airJumpChance = 0.5f;
    [Tooltip("공중점프가 솟아오르기 시작할 때 발밑(몸 충돌 판정의 아래쪽 가운데)에 띄우는 일회성 효과 프리팹입니다. Assets/Prefabs/FX/Mushroom/MushroomAirJumpDust를 연결합니다. 띄운 자리에 남고 Mushroom을 따라 올라가지 않습니다.\n비워 두면 효과가 나오지 않습니다.")]
    [SerializeField] private GameObject _airJumpDustPrefab;

    [Header("점프공격")]
    [Tooltip("점프공격의 준비 시간입니다. 이 동안 그 자리(공중이면 공중)에 멈춘 채 직전 그림(점프·발사의 마지막 그림)을 유지하며 플레이어 쪽을 봅니다. 이 시간이 지나면 기억해 둔 플레이어 쪽으로 돌진합니다. 점프 꼭대기에서 멈춰 기다리는 시간도 이것입니다. 0이면 바로 돌진합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _jumpAttackReadyDuration = 0.5f;
    [Tooltip("점프공격의 준비가 얼마나 진행됐을 때 플레이어 위치를 기억할지 정합니다. 그 위치를 향해 돌진합니다. 0이면 준비를 시작하는 순간, 0.5면 준비 시간의 절반이 지났을 때, 1이면 준비가 끝나는 순간입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _jumpAttackAimTimeRatio = 0.5f;
    [Tooltip("점프공격으로 플레이어 쪽으로 돌진하는 속도입니다. 벽·땅·천장에 닿을 때까지 일정합니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _jumpAttackSpeed = 30f;
    [Tooltip("점프공격이 벽·땅·천장에 닿지 못하는 예외 상황에서 돌진을 끝내는 최대 시간입니다. (단위: 초)")]
    [SerializeField, Min(0.1f)] private float _jumpAttackMaxDuration = 2f;
    [Tooltip("돌진하는 동안 몸에 닿은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 한 번의 점프공격에서 한 번만 맞습니다. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _jumpAttackDamageMultiplier = 1f;
    [Tooltip("돌진하는 동안(JumpAttack 클립의 앞 3장) 그림을 뒤쪽(돌진 반대쪽)으로 옮기는 거리입니다. 이 그림들은 칸의 앞쪽에 치우쳐 그려져 있어, 옮기지 않으면 몸 충돌 판정보다 앞에 보입니다. 벽·땅·천장에 닿으면 원래 자리로 돌아갑니다. (단위: 월드 거리)")]
    [SerializeField] private float _jumpAttackSpriteOffsetX = 1.25f;
    [Tooltip("점프공격이 벽·땅·천장에 부딪혔을 때 재생하는 피드백입니다. (카메라 흔들림, SkullDog 벽 충돌·Knight 슬라이딩·BloodWing 대시 충돌과 같은 값) 루트 VFX/Impact/ImpactFeedbacks의 MMF_Player를 연결합니다.\n시간이 다 되어 끝날 때는 재생하지 않습니다. 비워 두면 흔들리지 않습니다.")]
    [SerializeField] private MMF_Player _impactFeedbacks;
    [Tooltip("점프공격이 벽·땅·천장에 부딪혔을 때 재생할 효과음 데이터입니다. (Knight 드롭킥·SoulKnight 슬라이딩 벽 충돌과 같은 Assets/Data/Sound/DropKickImpact_Knight) SoundManager의 Sfx 채널로 재생하며, 위 피드백과 같은 때 재생합니다.\n비워 두면 소리 없이 진행합니다.")]
    [SerializeField] private SoundDataSO _impactSound;
    [Tooltip("점프공격이 벽·땅·천장에 부딪힌 자리에 띄우는 충격파 효과 프리팹입니다. Assets/Prefabs/FX/Mushroom/MushroomJumpAttackImpact(pixelFX heavy-landing을 절반 크기로 둔 프리팹 변형)를 연결합니다. 부딪힌 면 쪽으로 회전해 벽이면 옆으로, 천장이면 아래로 퍼집니다.\n비워 두면 효과는 나오지 않지만 충격파 판정은 그대로 나갑니다.")]
    [SerializeField] private GameObject _impactWavePrefab;
    [Tooltip("충격파 판정이 살아 있는 시간입니다. 효과의 고리가 퍼지는 시간(0.33초)에 맞춰 두었습니다. 그 뒤에 남는 먼지에는 피해가 없습니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _impactWaveDuration = 0.33f;
    [Tooltip("충격파 판정의 처음 반폭입니다. 부딪힌 면을 따라 양쪽으로 이만큼 퍼진 상태에서 시작합니다. 절반 크기 효과의 첫 고리에 맞춘 값입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _impactWaveStartHalfWidth = 0.7f;
    [Tooltip("충격파 판정의 마지막 반폭입니다. Impact Wave Duration 동안 처음 반폭에서 이 값까지 처음엔 빠르고 갈수록 느리게 퍼집니다. 절반 크기 효과의 고리가 가장 넓을 때(폭 약 4)에 맞춘 값입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _impactWaveEndHalfWidth = 2f;
    [Tooltip("충격파 판정의 처음 높이입니다. 부딪힌 면에서 바깥쪽(땅이면 위, 벽이면 벽 반대쪽)으로의 두께입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0.01f)] private float _impactWaveStartHeight = 0.15f;
    [Tooltip("충격파 판정의 마지막 높이입니다. 반폭과 같은 곡선으로 처음 높이에서 이 값까지 커집니다. 절반 크기 효과의 먼지 고리 높이(약 0.9)에 맞춘 값입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0.01f)] private float _impactWaveEndHeight = 0.9f;

    [Header("패턴 게이지")]
    [Tooltip("패턴 게이지가 0%에서 100%까지 저절로 차는 데 걸리는 시간입니다. 반응속도 기믹과 스턴 동안, 죽었거나 플레이어가 죽어 있는 동안에는 차지 않습니다. (단위: 초)")]
    [SerializeField, Min(0.1f)] private float _gaugeFillDuration = 25f;
    [Tooltip("런·발사·점프공격의 몸 판정이나 점프공격 충격파로 플레이어에게 피해를 입혔을 때 패턴 게이지가 즉시 차는 양입니다. 게이지 최대값에 대한 비율입니다. 0.1이면 10%가 찹니다. 기믹의 발사로 입힌 피해에는 차지 않습니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _hitGaugeGainRatio = 0.1f;

    [Header("반응속도 기믹 (패턴 게이지 100%)")]
    [Tooltip("기믹 동안 줌인한 카메라의 직교 크기(화면 세로 절반의 월드 길이)입니다. 원래 크기(5)보다 작을수록 더 확대됩니다. 카메라는 화면 세로 가운데에 Mushroom의 몸 가운데가 오도록 내려가므로, 값을 키우면 바닥 그림 아래가 검은 띠 밖으로 보일 수 있습니다.")]
    [SerializeField, Min(0.1f)] private float _reactionCameraSize = 3.5f;
    [Tooltip("기믹 동안 Mushroom을 줌인한 화면의 한쪽 끝에서 안쪽으로 띄워 두는 거리입니다. 화면 가장자리에서 몸 가운데까지의 거리입니다. 준비자세 그림이 몸보다 뒤로 젖혀져 있어, 너무 작으면 갓이 화면 밖으로 잘립니다. 클수록 플레이어와 가까워져 반응할 시간이 줄어듭니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _reactionSelfEdgeMargin = 2.5f;
    [Tooltip("기믹 동안 플레이어를 줌인한 화면의 반대쪽 끝에서 안쪽으로 띄워 두는 거리입니다. 화면 가장자리에서 플레이어 가운데까지의 거리입니다. 클수록 Mushroom과 가까워져 반응할 시간이 줄어듭니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _reactionTargetEdgeMargin = 1.5f;
    [Tooltip("기믹을 시작할 때 Mushroom과 플레이어를 양쪽 자리로 미끄러지듯 옮기는 시간입니다. 카메라 줌 시간(CinematicManager의 피드백, 0.4초)과 맞춰 둡니다. 플레이어가 공중이면 착지할 때까지 더 기다립니다. (단위: 초)")]
    [SerializeField, Min(0.02f)] private float _reactionSetupDuration = 0.4f;
    [Tooltip("자리 배치가 끝난 뒤 발사 준비자세로 기다리는 시간의 최소값입니다. 실제 시간은 기믹마다 이 값과 Reaction Max Wait 사이에서 무작위로 정합니다. 이 동안 플레이어는 공격만 할 수 있습니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _reactionMinWait = 2f;
    [Tooltip("발사 준비자세로 기다리는 시간의 최대값입니다. Reaction Min Wait보다 작으면 최소값으로 봅니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _reactionMaxWait = 10f;
    [Tooltip("기믹에서 플레이어에게 날아가는 속도입니다. 빠를수록 플레이어가 반응할 시간이 줄어듭니다. 일반 발사(Launch Speed 30)로는 성공할 수 있는 시간이 너무 짧아 절반으로 낮춘 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _reactionLaunchSpeed = 15f;
    [Tooltip("기믹의 발사가 플레이어에게 닿았을 때(플레이어가 먼저 때리지 못했을 때) 입히는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값.")]
    [SerializeField, Min(0f)] private float _reactionDamageMultiplier = 3f;

    [Header("반응속도 기믹의 결과 연출 (맞히거나 맞은 순간)")]
    [Tooltip("기믹에서 Mushroom이 플레이어를 맞히거나 플레이어에게 맞은 순간, 맞은 지점을 중심으로 화면을 덮는 임팩트 프레임 효과 프리팹입니다. Assets/Prefabs/FX/Mushroom/MushroomReactionImpactFrame(pixelFX impacts/impact-frame)을 연결합니다. 실제 시간으로 재생됩니다.\n비워 두면 효과가 나오지 않습니다.")]
    [SerializeField] private GameObject _reactionImpactFramePrefab;
    [Tooltip("결과가 난 순간 재생하는 피드백입니다. 슬로우 모션(Timescale Modifier Shake, 0.3배속으로 실제 0.6초)과, 0.6초 뒤 슬로우를 모두 해제하는 단계(Timescale Modifier Reset, Initial Delay 0.6)가 들어 있습니다. 해제 단계는 같은 순간 걸린 플레이어 피격 슬로우가 뒤에 이어져 길어지지 않게 합니다. 루트 VFX/Reaction/ReactionResultFeedbacks의 MMF_Player를 연결합니다. 배속과 길이는 그 피드백에서 조절하며, 길이를 바꾸면 해제 단계의 Initial Delay와 Reaction Result Hold Duration도 같이 맞춥니다.\n이 결과로 플레이어나 Mushroom이 죽으면 재생하지 않고 KO 연출의 슬로우 모션에 맡깁니다. 비워 두면 느려지지 않습니다.")]
    [SerializeField] private MMF_Player _reactionResultFeedbacks;
    [Tooltip("기믹에서 플레이어가 맞았을 때(실패) 재생하는 효과음 데이터입니다. Assets/Data/Sound/ReactionHit_Player(플레이어 KO 소리를 피치 1.2·볼륨 0.7로 바꾼 것)를 연결합니다. 실제로 죽을 때의 KO 소리와 구분되게 따로 둡니다. SoundManager의 Sfx 채널로 재생합니다.\n이 피해로 플레이어가 죽으면 KO 연출이 같은 소리를 내므로 여기서는 재생하지 않습니다. 비워 두면 소리 없이 진행합니다.")]
    [SerializeField] private SoundDataSO _reactionTargetHitSound;
    [Tooltip("기믹에서 Mushroom이 맞았을 때(성공) 재생하는 효과음 데이터입니다. Assets/Data/Sound/ReactionHit_Mushroom(보스 KO 소리를 피치 1.2·볼륨 0.7로 바꾼 것)을 연결합니다. 실제로 죽을 때의 KO 소리와 구분되게 따로 둡니다. SoundManager의 Sfx 채널로 재생합니다.\n이 피해로 Mushroom이 죽으면 KO 연출이 같은 소리를 내므로 여기서는 재생하지 않습니다. 비워 두면 소리 없이 진행합니다.")]
    [SerializeField] private SoundDataSO _reactionSelfHitSound;
    [Tooltip("결과가 난 뒤 시네마틱(줌인한 화면, 검은 띠, 조작 잠금)을 유지하는 시간입니다. 슬로우 모션 길이(실제 0.6초)에 맞춰 두었고, 지나면 카메라가 원래대로 돌아갑니다. 실제 시간이며 일시정지 중에는 흐르지 않습니다. (단위: 초)\n이 결과로 플레이어나 Mushroom이 죽으면 기다리지 않고 바로 KO 연출로 넘어갑니다.")]
    [SerializeField, Min(0f)] private float _reactionResultHoldDuration = 0.6f;

    [Header("반응속도 기믹의 준비 표시 (준비자세 동안)")]
    [Tooltip("발사 준비자세로 기다리기 시작할 때 갓 앞에 노란 기 모으기 효과(pixelFX arcane/spell-charge, 노란색)를 켜는 피드백입니다. Visuals/MirroredVFX/Reaction/ReadyCharge_Enter의 MMF_Player를 연결합니다.\n비워 두면 준비 표시가 나오지 않습니다.")]
    [SerializeField] private MMF_Player _reactionChargeEnter;
    [Tooltip("발사하기 시작할 때나 기믹이 중간에 끊길 때 기 모으기 효과를 끄는 피드백입니다. Visuals/MirroredVFX/Reaction/ReadyCharge_Exit의 MMF_Player를 연결합니다.")]
    [SerializeField] private MMF_Player _reactionChargeExit;

    [Header("스턴 (기믹에서 플레이어가 먼저 때렸을 때)")]
    [Tooltip("스턴에 걸려 있는 시간입니다. 반응속도 기믹의 발사 도중, 플레이어에게 닿기 전에 플레이어에게 피해를 입으면 걸립니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _stunDuration = 5f;
    [Tooltip("스턴하는 동안 머리 위(StatusVfxDisplay의 Anchor)에 띄울 VFX 프리팹입니다. 모든 보스가 함께 쓰는 Assets/Prefabs/FX/StatusVfx_Stun을 연결합니다.\n비워 두면 VFX 없이 스턴합니다.")]
    [SerializeField] private GameObject _stunVfxPrefab;

    // 이동을 멈추는 지형(바닥, 벽)을 찾는 조건
    private ContactFilter2D _obstacleFilter;
    private readonly RaycastHit2D[] _castResults = new RaycastHit2D[MaxCastCount];
    // 남은 공중점프 횟수. 땅에 닿아 있는 동안 Air Jump Count로 다시 채운다.
    private int _remainingAirJumps;
    // 메인 카메라. 반응속도 기믹의 자리 배치(화면 비율)와 충격 프레임 효과의 크기를 맞추는 데 쓴다.
    private Camera _mainCamera;
    // 메인 카메라를 찾지 못했다는 경고를 한 번만 내기 위한 표시
    private bool _hasWarnedNoCamera;
    // 전투 시작 때 찾은 양쪽 벽(Wall 레이어)의 안쪽 면 x. 조준점이 벽에 끼지 않게 가로 범위를 자르는 데 쓴다. 벽을 찾지 못하면 제한하지 않는다.
    private float _leftWallX = float.NegativeInfinity;
    private float _rightWallX = float.PositiveInfinity;
    // 점프공격 충격파 판정이 살아 있는지와 그 자리. (StartImpactWave / UpdateImpactWave)
    // 원점은 부딪힌 면 위의 점, 위쪽 방향은 면에서 바깥쪽(땅이면 위, 왼쪽 벽이면 오른쪽), 각도는 위쪽 방향에 맞춰 돌린 각도(도)다.
    private bool _isImpactWaveActive;
    private Vector2 _impactWaveOrigin;
    private Vector2 _impactWaveUp;
    private float _impactWaveAngle;
    private float _impactWaveStartTime;
    // 반응속도 기믹의 시네마틱을 열어 둔 상태인지 여부. 연 쪽이 반드시 닫아야 하므로 기억해 둔다. (BeginReactionCinematic / EndReactionCinematic)
    private bool _isReactionCinematicPlaying;
    // 기믹의 결과가 난 뒤 시네마틱을 더 유지할 남은 시간(실제 시간). 0보다 크면 상태가 끝나도 시네마틱을 닫지 않고, 다 흐르면 OnUpdate가 닫는다.
    private float _reactionCinematicHoldTime;

    public MushroomRunState RunState { get; private set; }
    public MushroomLaunchReadyState LaunchReadyState { get; private set; }
    public MushroomLaunchState LaunchState { get; private set; }
    public MushroomJumpState JumpState { get; private set; }
    public MushroomJumpAttackState JumpAttackState { get; private set; }
    public MushroomFallState FallState { get; private set; }
    public MushroomStunState StunState { get; private set; }
    public MushroomReactionState ReactionState { get; private set; }

    public HitboxShape BodyHitbox => _bodyHitbox;
    public float RunReadyDuration => _runReadyDuration;
    public float RunAimTime => _runReadyDuration * _runAimTimeRatio;
    public float RunSpeed => _runSpeed;
    public float RunMaxDuration => _runMaxDuration;
    public float RunDamage => Stats.AttackPower * _runDamageMultiplier;
    public float LaunchReadyDuration => _launchReadyDuration;
    public float LaunchAimTime => _launchReadyDuration * _launchAimTimeRatio;
    public float LaunchSpeed => _launchSpeed;
    public float LaunchMaxDuration => _launchMaxDuration;
    public float LaunchDamage => Stats.AttackPower * _launchDamageMultiplier;
    public float JumpHeight => _jumpHeight;
    public float JumpReadyDuration => _jumpReadyDuration;
    public float JumpRiseDuration => _jumpRiseDuration;
    public float JumpEndSpeedRatio => _jumpEndSpeedRatio;
    public float AirJumpHeight => _jumpHeight * _airJumpHeightRatio;
    public float AirJumpReadyDuration => _airJumpReadyDuration;
    public float AirJumpRiseDuration => _airJumpRiseDuration;
    public float JumpAttackReadyDuration => _jumpAttackReadyDuration;
    public float JumpAttackAimTime => _jumpAttackReadyDuration * _jumpAttackAimTimeRatio;
    public float JumpAttackSpeed => _jumpAttackSpeed;
    public float JumpAttackMaxDuration => _jumpAttackMaxDuration;
    public float JumpAttackDamage => Stats.AttackPower * _jumpAttackDamageMultiplier;
    public float JumpAttackSpriteOffsetX => _jumpAttackSpriteOffsetX;
    public float StunDuration => _stunDuration;
    public GameObject StunVfxPrefab => _stunVfxPrefab;
    public float ReactionSetupDuration => _reactionSetupDuration;
    public float ReactionMinWait => _reactionMinWait;
    public float ReactionMaxWait => Mathf.Max(_reactionMinWait, _reactionMaxWait);
    public float ReactionLaunchSpeed => _reactionLaunchSpeed;
    public float ReactionDamage => Stats.AttackPower * _reactionDamageMultiplier;

    // 공중점프를 할 수 있는지 여부. 땅에 닿기 전까지 Air Jump Count번만 할 수 있다.
    public bool CanAirJump => _remainingAirJumps > 0;
    // 지금 점프를 시작할 수 있는지 여부. 땅에서는 언제나 뛸 수 있고, 공중에서는 공중점프가 남아 있어야 한다.
    private bool CanStartJump => Movement.IsGrounded || CanAirJump;
    // 플레이어가 공중에 있는지 여부. 땅에 닿아 있지 않으면 공중이다. (점프·낙하·벽타기 중) 플레이어가 없으면(죽음) false다.
    private bool IsTargetInAir => !IsTargetDead && !Target.Movement.IsGrounded;

    // 이번 런·발사로 플레이어에게 피해를 줬는지 여부. 런·발사를 시작할 때 비우고(ResetPlayerHit), 몸 판정이 피해를 줄 때 켠다(MarkPlayerHit).
    // 켜져 있으면 런·발사가 끝날 때 플레이어가 맞아서 떠 있더라도 땅에 있을 때처럼 다음 패턴을 고른다.
    private bool _hasHitPlayer;

    protected override bool OnInit(BattleContext context)
    {
        if (!base.OnInit(context))
        {
            return false;
        }

        if (PatternGauge == null)
        {
            Debug.LogError("Mushroom 루트에 PatternGauge가 없습니다. Mushroom은 패턴 게이지가 꽉 차면 반응속도 기믹을 시작합니다.", this);
            return false;
        }

        _obstacleFilter = new ContactFilter2D();
        _obstacleFilter.SetLayerMask(LayerMask.GetMask(GroundLayerName, WallLayerName));
        _obstacleFilter.useTriggers = false;

        _remainingAirJumps = _airJumpCount;
        _isReactionCinematicPlaying = false;
        _reactionCinematicHoldTime = 0f;
        _isImpactWaveActive = false;

        _mainCamera = Camera.main;

        int wallLayerMask = LayerMask.GetMask(WallLayerName);
        Vector2 bodyCenter = BodyCollider.bounds.center;
        RaycastHit2D leftWallHit = Physics2D.Raycast(bodyCenter, Vector2.left, WallSearchDistance, wallLayerMask);
        RaycastHit2D rightWallHit = Physics2D.Raycast(bodyCenter, Vector2.right, WallSearchDistance, wallLayerMask);
        _leftWallX = leftWallHit.collider != null ? leftWallHit.point.x : float.NegativeInfinity;
        _rightWallX = rightWallHit.collider != null ? rightWallHit.point.x : float.PositiveInfinity;

        if (_stunVfxPrefab != null)
        {
            if (StatusVfx == null)
            {
                Debug.LogWarning("Mushroom 루트에 StatusVfxDisplay가 없어 스턴 VFX를 띄우지 않습니다.", this);
            }
            else if (PoolManager.HasInstance)
            {
                // 첫 스턴 때 생성 비용으로 끊기지 않도록 미리 하나 만들어 둔다.
                PoolManager.Instance.Prewarm(_stunVfxPrefab, 1);
            }
        }

        if (_airJumpDustPrefab != null && PoolManager.HasInstance)
        {
            // 첫 공중점프 때 생성 비용으로 끊기지 않도록 미리 하나 만들어 둔다.
            PoolManager.Instance.Prewarm(_airJumpDustPrefab, 1);
        }

        if (_impactWavePrefab != null && PoolManager.HasInstance)
        {
            // 첫 점프공격 충돌 때 생성 비용으로 끊기지 않도록 미리 하나 만들어 둔다.
            PoolManager.Instance.Prewarm(_impactWavePrefab, 1);
        }

        if (_reactionImpactFramePrefab != null && PoolManager.HasInstance)
        {
            // 첫 기믹 결과 때 생성 비용으로 끊기지 않도록 미리 하나 만들어 둔다.
            PoolManager.Instance.Prewarm(_reactionImpactFramePrefab, 1);
        }

        if (_reactionResultFeedbacks != null)
        {
            _reactionResultFeedbacks.Initialization();
        }

        // 켜는 피드백은 초기화할 때 기 모으기 효과를 꺼 둔다.
        if (_reactionChargeEnter != null)
        {
            _reactionChargeEnter.Initialization();
        }

        if (_reactionChargeExit != null)
        {
            _reactionChargeExit.Initialization();
        }

        // 켜는 피드백은 초기화할 때 먼지를 꺼 둔다.
        if (_runDustEnter != null)
        {
            _runDustEnter.Initialization();
        }

        if (_runDustExit != null)
        {
            _runDustExit.Initialization();
        }

        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.Initialization();
        }

        // 켜는 피드백은 초기화할 때 속도선을 꺼 둔다.
        if (_speedLinesEnter != null)
        {
            _speedLinesEnter.Initialization();
        }

        if (_speedLinesExit != null)
        {
            _speedLinesExit.Initialization();
        }

        return true;
    }

    protected override void OnInitCompleted()
    {
        base.OnInitCompleted();
        Vital.Damaged += OnDamaged;
    }

    // 기믹 도중에 전투가 끝나도(씬 전환 등) 시네마틱이 열린 채로 남지 않게 닫는다.
    protected override void OnRelease()
    {
        Vital.Damaged -= OnDamaged;
        _isImpactWaveActive = false;
        EndReactionCinematic();

        if (_reactionResultFeedbacks != null)
        {
            _reactionResultFeedbacks.StopFeedbacks();
        }
        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.StopFeedbacks();
        }
        base.OnRelease();
    }

    // 피해를 입었을 때. 반응속도 기믹 중이면 그 상태가 성공(스턴) 여부를 판단한다. 플레이어가 준 피해만 센다.
    // 이 피해로 죽는 경우에도 알린다. (결과 연출의 임팩트 프레임은 보여 주고, 스턴 대신 사망·KO 연출로 넘어간다)
    private void OnDamaged(DamageInfo damageInfo)
    {
        if (CurrentState != ReactionState)
        {
            return;
        }

        if (Target == null || damageInfo.Instigator != Target.gameObject)
        {
            return;
        }

        ReactionState.OnDamagedByTarget(damageInfo.HitPoint);
    }

    // 런을 시작할 때 MushroomRunState가 호출한다. 발밑 먼지(Visuals/MirroredVFX/Run/RunDust)를 켠다. 보스 방향에 따라 같이 뒤집힌다.
    public void PlayRunDust()
    {
        if (_runDustEnter != null)
        {
            _runDustEnter.PlayFeedbacks();
        }
    }

    // 런이 끝나거나 끊길 때 MushroomRunState가 호출한다. 발밑 먼지를 끈다.
    public void StopRunDust()
    {
        if (_runDustExit != null)
        {
            _runDustExit.PlayFeedbacks();
        }
    }

    // 런·발사·점프공격으로 빠르게 움직이기 시작할 때 각 상태가 호출한다. 속도선(Visuals/MirroredVFX/Speed/SpeedLines)을
    // 움직이는 방향으로 기울여 켠다. 보스 방향에 따라 같이 뒤집히므로, 보는 쪽을 +x로 둔 각도(atan2(y, |x|))만 정한다. (SkullDog 대시와 같다)
    public void PlaySpeedLines(Vector2 direction)
    {
        if (_speedLines != null)
        {
            float angle = Mathf.Atan2(direction.y, Mathf.Abs(direction.x)) * Mathf.Rad2Deg;
            _speedLines.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (_speedLinesEnter != null)
        {
            _speedLinesEnter.PlayFeedbacks();
        }
    }

    // 빠른 움직임이 끝나거나 끊길 때 각 상태가 호출한다. 속도선을 끈다.
    public void StopSpeedLines()
    {
        if (_speedLinesExit != null)
        {
            _speedLinesExit.PlayFeedbacks();
        }
    }

    // 점프공격이 벽·땅·천장에 부딪혔을 때 MushroomJumpAttackState가 호출한다. 효과음과 피드백(카메라 흔들림)을 재생한다.
    public void PlayImpactFeedbacks()
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

    // 점프공격이 벽·땅·천장에 부딪혔을 때 MushroomJumpAttackState가 호출한다. 부딪힌 자리에 충격파 효과(pixelFX heavy-landing)를 띄우고
    // 같은 범위의 판정을 Impact Wave Duration 동안 낸다. (UpdateImpactWave) 효과와 판정은 Mushroom을 따라가지 않고 그 자리에 남는다.
    // contactNormal: 부딪힌 면의 법선(면에서 바깥쪽). 가장 가까운 축(땅이면 위, 벽이면 옆, 천장이면 아래)으로 맞춘 뒤, 효과의 위쪽이 그 방향을 향하도록 돌린다.
    // 원점은 몸 콜라이더에서 그 면에 맞닿은 쪽 가장자리의 가운데다. (땅이면 발밑 가운데, 왼쪽 벽이면 몸 왼쪽 가운데)
    public void StartImpactWave(Vector2 contactNormal)
    {
        Vector2 up = Mathf.Abs(contactNormal.x) > Mathf.Abs(contactNormal.y)
            ? new Vector2(Mathf.Sign(contactNormal.x), 0f)
            : new Vector2(0f, contactNormal.y >= 0f ? 1f : -1f);

        Bounds bodyBounds = BodyCollider.bounds;
        _impactWaveOrigin = (Vector2)bodyBounds.center - new Vector2(up.x * bodyBounds.extents.x, up.y * bodyBounds.extents.y);
        _impactWaveUp = up;
        // 효과의 위쪽(0, 1)을 up으로 돌리는 각도. 왼쪽 벽(법선 오른쪽)이면 -90도다.
        _impactWaveAngle = Mathf.Atan2(-up.x, up.y) * Mathf.Rad2Deg;
        _impactWaveStartTime = Time.time;
        _isImpactWaveActive = true;

        if (_impactWavePrefab != null && PoolManager.HasInstance)
        {
            PoolManager.Instance.Spawn(_impactWavePrefab, _impactWaveOrigin, Quaternion.Euler(0f, 0f, _impactWaveAngle));
        }
    }

    // 충격파 판정을 낸다. 부딪힌 면을 따라 반폭이, 바깥쪽으로 높이가 처음엔 빠르고 갈수록 느리게(ease-out) 커진다. (효과의 고리가 퍼지는 모양에 맞춤)
    // 점프공격 몸통과 같은 판정 기록을 쓰므로, 이번 점프공격에서 몸통에 이미 맞은 플레이어는 충격파에 다시 맞지 않는다. (한 번만 맞는다)
    // 맞히면 패턴 게이지가 찬다. Mushroom이 죽거나 반응속도 기믹이 시작되면 판정을 거둔다.
    private void UpdateImpactWave()
    {
        if (!_isImpactWaveActive)
        {
            return;
        }

        float elapsedTime = Time.time - _impactWaveStartTime;

        // 반응속도 기믹이 시작되면(점프공격 마무리가 0.33초보다 먼저 끝나고 게이지가 꽉 찬 경우) 남은 판정을 거둔다.
        // 기믹이 비운 게이지가 다시 차거나, 자리로 미끄러지는 플레이어가 맞지 않게 한다.
        if (Vital.IsDead || CurrentState == ReactionState || elapsedTime > _impactWaveDuration)
        {
            _isImpactWaveActive = false;
            return;
        }

        float progress = elapsedTime / _impactWaveDuration;
        float eased = 1f - (1f - progress) * (1f - progress);
        float halfWidth = Mathf.Lerp(_impactWaveStartHalfWidth, _impactWaveEndHalfWidth, eased);
        float height = Mathf.Lerp(_impactWaveStartHeight, _impactWaveEndHeight, eased);
        Vector2 center = _impactWaveOrigin + _impactWaveUp * (height * 0.5f);

        if (HitChecker.Hit(center, new Vector2(halfWidth * 2f, height), _impactWaveAngle, JumpAttackDamage) > 0)
        {
            AddHitGauge();
        }
    }

    // 공중점프가 솟아오르기 시작할 때 MushroomJumpState가 호출한다. 발밑 자리에 효과(pixelFX impacts/air-jump)를 띄운다.
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

    protected override void CreateStates()
    {
        base.CreateStates();
        RunState = new MushroomRunState(this, stateMachine);
        LaunchReadyState = new MushroomLaunchReadyState(this, stateMachine);
        LaunchState = new MushroomLaunchState(this, stateMachine);
        JumpState = new MushroomJumpState(this, stateMachine);
        JumpAttackState = new MushroomJumpAttackState(this, stateMachine);
        FallState = new MushroomFallState(this, stateMachine);
        StunState = new MushroomStunState(this, stateMachine);
        ReactionState = new MushroomReactionState(this, stateMachine);
    }

    protected override void OnUpdate()
    {
        // 땅에 닿아 있는 동안 공중점프 횟수를 다시 채운다. (플레이어와 같은 규칙)
        if (Movement.IsGrounded)
        {
            _remainingAirJumps = _airJumpCount;
        }

        UpdateImpactWave();
        UpdateReactionCinematicHold();

        // 패턴 게이지는 Gauge Fill Duration에 걸쳐 0%에서 100%까지 일정하게 찬다. 기믹·스턴 중이거나 죽었거나 플레이어가 죽어 있으면 차지 않는다.
        if (Vital.IsDead || IsTargetDead || CurrentState == ReactionState || CurrentState == StunState)
        {
            return;
        }

        PatternGauge.Add(PatternGauge.MaxValue / _gaugeFillDuration * Time.deltaTime);
    }

    // Idle 중 매 프레임 호출된다. 패턴 게이지가 꽉 찼고 땅에 닿아 있으면 Idle 시간을 기다리지 않고 바로 반응속도 기믹을 시작한다.
    // (Idle이 아닌 상태에서는 꽉 차도 시작하지 않고, 그 패턴이 끝나 Idle에 들어갈 때 시작한다. 공중에서 Idle에 들어왔다면 착지한 뒤에 시작한다)
    public override bool TryInterruptIdle()
    {
        if (!PatternGauge.IsFull || !Movement.IsGrounded)
        {
            return false;
        }

        stateMachine.ChangeState(ReactionState);
        return true;
    }

    // 런·발사·점프공격의 몸 판정(각 상태)이나 점프공격 충격파(UpdateImpactWave)가 플레이어에게 피해를 줬을 때 호출한다. 패턴 게이지를 Hit Gauge Gain Ratio만큼 즉시 채운다.
    public void AddHitGauge()
    {
        if (Vital.IsDead)
        {
            return;
        }

        PatternGauge.Add(PatternGauge.MaxValue * _hitGaugeGainRatio);
    }

    // 반응속도 기믹의 자리 배치를 계산한다. 줌인한 화면의 양쪽 끝에서 안쪽으로 띄운 자리가 두 캐릭터의 자리다.
    // (Mushroom은 Reaction Self Edge Margin, 플레이어는 Reaction Target Edge Margin만큼 띄운다)
    //  - 화면 가로 가운데는 양쪽 벽의 가운데(벽을 못 찾았으면 지금 Mushroom과 플레이어의 가운데)다.
    //  - 지금 서 있는 쪽을 유지한다. (플레이어가 왼쪽에 있으면 왼쪽 자리)
    //  - 카메라 세로 가운데는 땅에 선 Mushroom의 몸 가운데 높이다.
    // mushroomX / targetX는 각자의 루트 위치 기준 가로 좌표다.
    public void GetReactionLayout(out float mushroomX, out float targetX, out Vector2 cameraCenter)
    {
        Bounds bodyBounds = BodyCollider.bounds;
        float targetCenterX = Target.BodyCollider.bounds.center.x;
        bool hasWalls = !float.IsInfinity(_leftWallX) && !float.IsInfinity(_rightWallX);
        float centerX = hasWalls ? (_leftWallX + _rightWallX) * 0.5f : (bodyBounds.center.x + targetCenterX) * 0.5f;

        float aspect = TryGetMainCamera(out Camera mainCamera) ? mainCamera.aspect : 16f / 9f;
        float halfScreenWidth = _reactionCameraSize * aspect;
        float selfOffsetX = Mathf.Max(0f, halfScreenWidth - _reactionSelfEdgeMargin);
        float targetOffsetX = Mathf.Max(0f, halfScreenWidth - _reactionTargetEdgeMargin);

        // 몸이 벽에 끼지 않는 범위로 자른다.
        if (hasWalls)
        {
            float maxOffsetX = Mathf.Max(0f, (_rightWallX - _leftWallX) * 0.5f - bodyBounds.extents.x - AimWallGap);
            selfOffsetX = Mathf.Min(selfOffsetX, maxOffsetX);
            targetOffsetX = Mathf.Min(targetOffsetX, maxOffsetX);
        }

        float mushroomSide = bodyBounds.center.x >= targetCenterX ? 1f : -1f;
        float mushroomCenterX = centerX + mushroomSide * selfOffsetX;
        float aimTargetCenterX = centerX - mushroomSide * targetOffsetX;

        mushroomX = mushroomCenterX + (Movement.Position.x - bodyBounds.center.x);
        targetX = aimTargetCenterX + (Target.Movement.Position.x - targetCenterX);
        cameraCenter = new Vector2(centerX, bodyBounds.center.y);
    }

    // 반응속도 기믹의 시네마틱을 연다. 카메라는 cameraCenter를 가운데로 Reaction Camera Size까지 줌인한다.
    // 검은 띠·전투 UI(CinematicUI)와 플레이어 입력 잠금(Player)도 같은 이벤트로 처리된다. 이미 열려 있으면 다시 열지 않는다.
    public void BeginReactionCinematic(Vector2 cameraCenter)
    {
        // 앞선 기믹의 결과 연출로 남은 유지 시간이 새 기믹에 이어지지 않게, 이미 열려 있는 경우에도 비운다.
        _reactionCinematicHoldTime = 0f;

        if (_isReactionCinematicPlaying)
        {
            return;
        }

        _isReactionCinematicPlaying = true;
        EventBus<CinematicStartedEvent>.Publish(new CinematicStartedEvent(gameObject, cameraCenter, _reactionCameraSize));
    }

    // 반응속도 기믹의 시네마틱을 닫는다. 열려 있지 않으면 아무것도 하지 않는다.
    // 플레이어나 Mushroom이 죽어서 끝난 경우에는 KO 연출이 카메라를 이어받으므로 카메라는 되돌리지 않게 한다.
    public void EndReactionCinematic()
    {
        if (!_isReactionCinematicPlaying)
        {
            return;
        }

        _isReactionCinematicPlaying = false;
        _reactionCinematicHoldTime = 0f;
        bool keepsCamera = Vital.IsDead || IsTargetDead;
        EventBus<CinematicEndedEvent>.Publish(new CinematicEndedEvent(gameObject, keepsCamera));
    }

    // 발사 준비자세로 기다리기 시작할 때 MushroomReactionState가 호출한다. 갓 앞에 노란 기 모으기 효과(Visuals/MirroredVFX/Reaction/ReadyCharge)를 켠다.
    // 보스 방향에 따라 같이 뒤집혀 늘 플레이어 쪽(날아갈 쪽)에 보인다.
    public void PlayReactionCharge()
    {
        if (_reactionChargeEnter != null)
        {
            _reactionChargeEnter.PlayFeedbacks();
        }
    }

    // 발사하기 시작할 때나 기믹이 끊길 때 MushroomReactionState가 호출한다. 기 모으기 효과를 끈다.
    public void StopReactionCharge()
    {
        if (_reactionChargeExit != null)
        {
            _reactionChargeExit.PlayFeedbacks();
        }
    }

    // 기믹 상태가 끝날 때 MushroomReactionState가 호출한다. 결과 연출로 시네마틱을 유지할 시간이 남아 있으면 닫지 않고 두며,
    // 그 시간이 다 흐르면 UpdateReactionCinematicHold가 닫는다. 결과 없이 끊겼거나 누군가 죽었으면 바로 닫는다.
    public void EndReactionCinematicAfterResult()
    {
        if (_reactionCinematicHoldTime > 0f && !Vital.IsDead && !IsTargetDead)
        {
            return;
        }

        EndReactionCinematic();
    }

    // 기믹의 결과가 난 순간(Mushroom이 플레이어를 맞혔거나 플레이어에게 맞았을 때) MushroomReactionState가 호출한다.
    //  - 맞은 지점을 중심으로 임팩트 프레임을 띄운다.
    //  - 맞은 쪽의 기믹 전용 효과음(KO 소리를 변형한 것)을 내고 슬로우 모션 피드백을 재생한다. 그동안(Reaction Result Hold Duration) 시네마틱을 유지하고 플레이어의 공격 허용을 끈다.
    //  - 이 결과로 플레이어나 Mushroom이 죽었다면 임팩트 프레임만 띄운다. 소리·슬로우 모션·카메라는 KO 연출(KOEffectManager)이 맡아, 기믹 소리와 KO 소리가 겹치지 않는다.
    // isTargetHit: 플레이어가 맞았으면 true(실패), Mushroom이 맞았으면 false(성공)
    public void PlayReactionResult(Vector2 hitPoint, bool isTargetHit)
    {
        SpawnReactionImpactFrame(hitPoint);

        if (Vital.IsDead || IsTargetDead)
        {
            return;
        }

        SoundDataSO sound = isTargetHit ? _reactionTargetHitSound : _reactionSelfHitSound;

        if (sound != null && SoundManager.HasInstance)
        {
            SoundManager.Instance.PlaySfx(sound);
        }

        if (_reactionResultFeedbacks != null)
        {
            _reactionResultFeedbacks.PlayFeedbacks();
        }

        _reactionCinematicHoldTime = _reactionResultHoldDuration;
        Target.SetCinematicAttackAllowed(false, true);
    }

    // 임팩트 프레임을 풀에서 꺼내 hitPoint에 띄운다. 그림이 화면보다 크므로, 화면 가장자리가 비지 않는 범위(여유 ImpactFrameCoverMargin 포함) 안으로 중심을 옮긴다.
    // (그림이 화면보다 작은 축은 화면 가운데에 맞춘다) 재생이 끝나면 Flipbook이 풀로 돌려보낸다.
    private void SpawnReactionImpactFrame(Vector2 hitPoint)
    {
        if (_reactionImpactFramePrefab == null || !PoolManager.HasInstance)
        {
            return;
        }

        GameObject effect = PoolManager.Instance.Spawn(_reactionImpactFramePrefab, hitPoint, Quaternion.identity);

        if (effect == null || !TryGetMainCamera(out Camera mainCamera) || !effect.TryGetComponent(out SpriteRenderer effectRenderer))
        {
            return;
        }

        Vector2 cameraCenter = mainCamera.transform.position;
        Vector2 effectExtents = effectRenderer.bounds.extents;
        float slackX = Mathf.Max(0f, effectExtents.x - mainCamera.orthographicSize * mainCamera.aspect - ImpactFrameCoverMargin);
        float slackY = Mathf.Max(0f, effectExtents.y - mainCamera.orthographicSize - ImpactFrameCoverMargin);
        float x = Mathf.Clamp(hitPoint.x, cameraCenter.x - slackX, cameraCenter.x + slackX);
        float y = Mathf.Clamp(hitPoint.y, cameraCenter.y - slackY, cameraCenter.y + slackY);
        effect.transform.position = new Vector3(x, y, 0f);
    }

    // 결과 연출 동안 유지하던 시네마틱을 시간이 다 흐르면 닫는다. 실제 시간으로 세며 일시정지(timeScale 0) 중에는 세지 않는다.
    // 기믹 상태가 아직 끝나지 않았으면 닫지 않고, 그 상태가 끝날 때 닫게 둔다. 그사이 누군가 죽으면 바로 닫아 KO 연출에 카메라를 넘긴다.
    private void UpdateReactionCinematicHold()
    {
        if (!_isReactionCinematicPlaying || _reactionCinematicHoldTime <= 0f)
        {
            return;
        }

        if (Time.timeScale > 0f)
        {
            _reactionCinematicHoldTime -= Time.unscaledDeltaTime;
        }

        // 결과 연출 도중에 누군가 죽으면 KO 연출의 슬로우 모션이 시작된다. 결과 연출의 "0.6초 뒤 슬로우 모두 해제"가
        // KO 슬로우까지 끊지 않도록 결과 연출 피드백을 멈춘다. (멈춰도 배속을 되돌리지 않는 설정이라 KO 슬로우는 그대로 이어진다)
        if (Vital.IsDead || IsTargetDead)
        {
            _reactionCinematicHoldTime = 0f;

            if (_reactionResultFeedbacks != null)
            {
                _reactionResultFeedbacks.StopFeedbacks();
            }
        }

        if (_reactionCinematicHoldTime <= 0f && CurrentState != ReactionState)
        {
            EndReactionCinematic();
        }
    }

    // Idle 시간이 끝났을 때 호출된다. (플레이어가 죽어 있는 동안에는 호출되지 않는다)
    // Idle Jump Chance 확률로 점프를 고르고, 아니면 지금 플레이어가 땅에 있으면 런, 공중에 있으면 발사를 시전한다.
    public override void ChangeToNextPattern()
    {
        if (CanStartJump && Random.value < _idleJumpChance)
        {
            stateMachine.ChangeState(JumpState);
        }
        else if (IsTargetInAir)
        {
            stateMachine.ChangeState(LaunchReadyState);
        }
        else
        {
            stateMachine.ChangeState(RunState);
        }
    }

    // Idle 시간(Pattern Interval)은 땅에 닿아 있는 동안에만 센다. 공중에서 Idle에 들어왔다면 착지한 순간부터 센다.
    public override bool CanCountIdleTime => Movement.IsGrounded;

    // 패턴이 끝난 뒤 Idle로 쉬어간다. (Pattern Interval) 공중이라면 먼저 떨어져서 착지한 뒤에 쉰다.
    public void ChangeToRest()
    {
        if (Movement.IsGrounded)
        {
            ChangeToIdle(_patternInterval);
        }
        else
        {
            stateMachine.ChangeState(FallState);
        }
    }

    // 런·발사를 시작할 때 MushroomRunState / MushroomLaunchState가 호출한다. 이번 패턴으로 플레이어를 맞혔다는 기록을 비운다.
    public void ResetPlayerHit()
    {
        _hasHitPlayer = false;
    }

    // 런·발사의 몸 판정이 플레이어에게 피해를 줬을 때 MushroomRunState / MushroomLaunchState가 호출한다.
    public void MarkPlayerHit()
    {
        _hasHitPlayer = true;
    }

    // 런이 끝났을 때 MushroomRunState가 호출한다. Run Follow Up Chance 확률로 다음 패턴을 바로 시전하고, 아니면 Idle로 쉰다.
    // 다음 패턴은 지금 플레이어가 공중에 있으면 발사, 땅에 있으면 점프다. 단, 이번 런으로 플레이어를 맞혔다면 맞아서 떠 있어도 땅에 있을 때처럼 점프(또는 Idle)다.
    public void ChangeAfterRun()
    {
        if (IsTargetDead || Random.value >= _runFollowUpChance)
        {
            ChangeToRest();
        }
        else if (IsTargetInAir && !_hasHitPlayer)
        {
            stateMachine.ChangeState(LaunchReadyState);
        }
        else if (CanStartJump)
        {
            stateMachine.ChangeState(JumpState);
        }
        else
        {
            ChangeToRest();
        }
    }

    // 발사가 끝났을 때 MushroomLaunchState가 호출한다. Launch Jump Chance 확률로 점프를 시전하고, 아니면 점프공격을 시전한다.
    // 공중에서 끝났다면 점프는 공중점프가 되며(MushroomJumpState가 판단한다), 공중점프가 남아 있지 않으면 항상 점프공격이다.
    // 단, 이번 발사로 플레이어를 맞혔다면 점프공격은 고르지 않고 같은 확률로 점프 또는 Idle(공중이면 낙하 뒤 Idle)이다.
    public void ChangeAfterLaunch()
    {
        if (IsTargetDead)
        {
            ChangeToRest();
            return;
        }

        if (_hasHitPlayer)
        {
            if (CanStartJump && Random.value < _launchJumpChance)
            {
                stateMachine.ChangeState(JumpState);
            }
            else
            {
                ChangeToRest();
            }

            return;
        }

        if (CanStartJump && Random.value < _launchJumpChance)
        {
            stateMachine.ChangeState(JumpState);
        }
        else
        {
            stateMachine.ChangeState(JumpAttackState);
        }
    }

    // 점프가 끝났을 때(정한 높이에 도달) MushroomJumpState가 호출한다.
    // 공중점프가 남아 있으면 Air Jump Chance 확률로 한 번 더 점프하고, 아니면 점프공격을 시전한다. 공중점프가 남아 있지 않으면 항상 점프공격이다.
    public void ChangeAfterJump()
    {
        if (IsTargetDead)
        {
            ChangeToRest();
            return;
        }

        if (CanAirJump && Random.value < _airJumpChance)
        {
            stateMachine.ChangeState(JumpState);
        }
        else
        {
            stateMachine.ChangeState(JumpAttackState);
        }
    }

    // 스턴에 건다. 스턴 상태로 바꿨으면 true를 반환한다. 죽었거나 페이즈 전환 중이면 아무것도 하지 않는다.
    // 반응속도 기믹에서 Mushroom이 닿기 전에 플레이어가 먼저 때렸을 때 MushroomReactionState가 호출한다.
    public bool ChangeToStun()
    {
        if (!IsInitialized || Vital.IsDead || IsPhaseTransitioning)
        {
            return false;
        }

        stateMachine.ChangeState(StunState);
        return true;
    }

    // 공중점프 횟수를 하나 쓴다. 공중에서 점프를 시작할 때 MushroomJumpState가 호출한다.
    public void ConsumeAirJump()
    {
        _remainingAirJumps = Mathf.Max(0, _remainingAirJumps - 1);
    }

    // 발사·점프공격이 향할 지점(루트 위치 기준). 지금 플레이어의 가로 위치이고, 높이는 머쉬룸의 발이 플레이어의 발 높이에 오는 자리다.
    // 플레이어가 바닥에 서 있으면 바닥을 따라 가로로, 공중에 있으면 그 높이를 향한다. 런은 이 지점의 가로 위치만 쓴다.
    // 가로 위치는 몸 콜라이더가 양쪽 벽에 끼지 않는 범위로 자른다. 벽에 붙은 플레이어를 겨눠도 몸이 벽에 먼저 걸려 멈추지 않고,
    // 벽 바로 앞까지 가서 몸 판정(몸보다 조금 넓다)으로 맞힌다. (BloodWing 대시 조준과 같은 방식)
    // 플레이어가 없으면(죽음) 지금 자리다.
    public Vector2 GetAimPoint()
    {
        Vector2 position = Movement.Position;

        if (IsTargetDead)
        {
            return position;
        }

        Bounds bodyBounds = BodyCollider.bounds;
        Bounds targetBounds = Target.BodyCollider.bounds;
        float wallMargin = bodyBounds.extents.x + AimWallGap;
        float aimCenterX = targetBounds.center.x;

        // 두 벽 사이가 몸보다 좁은 예외 상황에서는 자르지 않는다.
        if (_leftWallX + wallMargin <= _rightWallX - wallMargin)
        {
            aimCenterX = Mathf.Clamp(aimCenterX, _leftWallX + wallMargin, _rightWallX - wallMargin);
        }

        float aimX = aimCenterX + (position.x - bodyBounds.center.x);
        float aimY = targetBounds.min.y + (position.y - bodyBounds.min.y);
        return new Vector2(aimX, aimY);
    }

    // 조준점까지의 가로 차이(offsetX, 오른쪽이 양수)로 볼 방향(오른쪽 1, 왼쪽 -1)을 정한다.
    // 조준점이 거의 제자리면(벽 구석의 플레이어를 겨눠 조준점이 벽 앞으로 잘린 경우 등) 그 미세한 차이 대신 플레이어가 있는 쪽을 본다.
    // 플레이어가 없으면(죽음) 지금 보는 방향을 유지한다.
    public float GetFacingToward(float offsetX)
    {
        if (Mathf.Abs(offsetX) > FacingDeadZone)
        {
            return Mathf.Sign(offsetX);
        }

        return IsTargetDead ? Animation.FacingDirection : DirectionToTarget;
    }

    // 몸 콜라이더를 direction(단위 벡터)으로 distance만큼 옮길 때 벽·바닥에 막히는지 확인한다.
    // 막히면 true와 함께, 닿기 직전까지 옮길 수 있는 거리를 freeDistance로 준다. 안 막히면 freeDistance는 distance다.
    // 움직이는 방향을 마주 보는 면만 막힌 것으로 본다. (바닥을 따라 가로로 달릴 때 발밑 바닥은 막는 것이 아니다)
    public bool CastBody(Vector2 direction, float distance, out float freeDistance)
    {
        return CastBody(direction, distance, out freeDistance, out _);
    }

    // 위와 같고, 막혔다면 막은 면의 법선(면에서 바깥쪽)도 돌려준다. 막히지 않았으면 Vector2.zero다. (점프공격 충격파의 방향을 정하는 데 쓴다)
    public bool CastBody(Vector2 direction, float distance, out float freeDistance, out Vector2 hitNormal)
    {
        Bounds bounds = BodyCollider.bounds;
        Vector2 castSize = new Vector2(Mathf.Max(0.01f, bounds.size.x - CastShrink * 2f), Mathf.Max(0.01f, bounds.size.y - CastShrink * 2f));
        int hitCount = Physics2D.BoxCast(bounds.center, castSize, 0f, direction, _obstacleFilter, _castResults, distance + CastShrink + CastContactSkin);
        float nearest = float.MaxValue;
        Vector2 nearestNormal = Vector2.zero;

        for (int i = 0; i < hitCount; i++)
        {
            if (Vector2.Dot(_castResults[i].normal, direction) > BlockingNormalDot)
            {
                continue;
            }

            if (_castResults[i].distance < nearest)
            {
                nearest = _castResults[i].distance;
                nearestNormal = _castResults[i].normal;
            }
        }

        hitNormal = nearestNormal;

        if (nearest == float.MaxValue)
        {
            freeDistance = distance;
            return false;
        }

        freeDistance = Mathf.Clamp(nearest - CastShrink - CastContactSkin, 0f, distance);
        return true;
    }

    // 메인 카메라(MainCamera 태그)를 구한다. 전투 시작 때 찾지 못했으면 다시 찾고, 그래도 없으면 경고를 한 번 남기고 false를 반환한다.
    private bool TryGetMainCamera(out Camera mainCamera)
    {
        if (_mainCamera == null)
        {
            _mainCamera = Camera.main;
        }

        if (_mainCamera == null && !_hasWarnedNoCamera)
        {
            _hasWarnedNoCamera = true;
            Debug.LogWarning("Mushroom: 메인 카메라(MainCamera 태그)를 찾지 못했습니다. 기믹의 자리 배치는 16:9 화면을 가정하고, 충격 프레임 효과의 크기를 맞추지 않습니다.", this);
        }

        mainCamera = _mainCamera;
        return mainCamera != null;
    }

    protected override void DrawHitboxGizmos(float facingDirection)
    {
        DrawHitboxGizmo(_bodyHitbox, facingDirection, "런·발사·점프공격 몸 충돌");
    }
}

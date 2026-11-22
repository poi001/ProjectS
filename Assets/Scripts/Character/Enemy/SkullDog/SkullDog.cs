using MoreMountains.Feedbacks;
using UnityEngine;

// SkullDog(스컬독) 보스. 1페이즈만 있는 지상형 보스다.
// 흐름: Idle → (무작위) 대시 준비(DashReady) → 대시(Dash) → 대시 마무리(DashEnd) → (무작위) 대시 준비 또는 Idle,
//       또는 런(Run) → 벽 충돌 → 스턴(Stun) → Idle
//  - 대시: 준비를 시작하는 순간의 플레이어 위치를 기억해 두고, 준비가 끝나면 그 자리로 빠르게 일직선 돌진한다.
//    플레이어가 공중에 있었다면 공중으로 돌진한다. 돌진하는 동안 몸에 닿은 플레이어가 피해를 입는다. (한 번의 대시에서 한 번만)
//    대시가 끝나면 Dash Chain Chance 확률로 Idle 없이 이어서 대시한다. (첫 대시 포함 최대 Max Dash Chain번)
//    이어서 대시할 때는 끝난 자리(공중이면 공중)에서 중력 없이 떠 있는 채로 준비 자세부터 다시 한다.
//    이어서 대시하지 않으면 그 자리에서 중력으로 떨어지고, 착지하면 Idle로 쉬어간다.
//  - 런: 플레이어가 있는 쪽으로 벽에 부딪힐 때까지 계속 달린다. 처음에는 느리고 시간이 지날수록 빨라진다.
//    시작하고 Run Grace Duration(0.5초) 동안은 벽에 닿아도 스턴에 걸리지 않고 플레이어도 피해를 입지 않는다.
//    플레이어가 반대쪽으로 넘어가면 바로 돌아서지 못하고 미끄러지며 속도를 줄인 뒤 돌아선다. (관성)
//    달리는 동안 몸에 닿은 플레이어가 피해를 입는다. 플레이어의 피격 무적 시간이 끝난 뒤에도 닿아 있으면 다시 맞는다.
//  - 스턴: 런으로 벽에 부딪히면 걸린다. 부딪힌 순간의 속도가 빠를수록 오래 기절한다.
//  - 패턴 게이지: 기믹 중이 아니면 상태와 상관없이 Gauge Fill Duration(20초)에 걸쳐 0%에서 100%까지 일정하게 차고,
//    플레이어에게 피해를 입힐 때마다 Hit Gauge Gain(10%)만큼 더 찬다. 꽉 차면 게이지 UI가 튕기고 푸른색으로 깜빡인다. (공통 PatternGaugeBar)
//  - 기믹: 게이지가 꽉 찬 채로 Idle이면 바닥 가운데로 순간이동해 바로 하울링(Howl)을 한다. 하울링하는 동안에는 Ghost 레이어라 플레이어의 공격이 통과하고 게이지도 그대로다.
//    하울링이 끝나면 폭주 런과 분신이 시작되고, 그때부터 게이지가 Gimmick Drain Duration(20초)에 걸쳐 0%까지 줄어든다.
//    기믹 중에 플레이어가 스컬독을 때리면 Damaged Gauge Loss(5%)만큼 더 줄어 기믹이 빨리 끝난다. 기믹 중에는 게이지가 차지 않는다.
//     · 폭주 런: 런과 같지만 벽에 부딪혀도 스턴에 걸리지 않고 속도도 줄지 않는다. (SkullDogRunState가 IsGimmickActive를 보고 바꾼다)
//       게이지가 0%가 되면 달리던 그대로 일반 런이 되어, 다음에 벽에 부딪히면 스턴에 걸린다.
//     · 분신(SkullDogClone): 화면 테두리(바닥·양쪽 벽·위쪽)의 무작위 위치에 나타나 플레이어에게 대시하고 사라지기를 되풀이한다. 플레이어의 공격은 맞지 않는다.
//       위쪽 테두리는 화면 끝이 아니라 보스 패턴 게이지 바(UI) 아래 가장자리까지다. 분신이 UI에 가려지지 않게 한다.
//       대시를 끝낼 때마다 기믹이 끝났는지 확인해, 끝났으면 다시 나타나지 않는다. 스컬독이 죽으면 바로 파괴된다.
[RequireComponent(typeof(PatternGauge))]
public class SkullDog : Enemy
{
    private const string GroundLayerName = "Ground";
    private const string WallLayerName = "Wall";
    private const string GhostLayerName = "Ghost";
    // 분신이 돌아다닐 범위(벽·바닥·천장 안쪽)를 화면 가운데에서 찾는 최대 거리 (단위: 월드 거리)
    private const float FieldSearchDistance = 100f;
    // 돌진 중 지형을 찾을 때 한 번에 처리할 최대 결과 수
    private const int MaxCastCount = 4;
    // 돌진하다 지형에 닿을 때 콜라이더 사이에 남기는 간격 (단위: 월드 거리)
    private const float CastContactSkin = 0.02f;
    // 지형을 찾는 박스를 몸 콜라이더보다 사방으로 이만큼 줄인다. 발밑 바닥처럼 이미 맞닿아 있는 지형이 겹친 것으로 잡히지 않게 한다. (단위: 월드 거리)
    private const float CastShrink = 0.02f;
    // 닿은 면이 움직이는 방향을 이 값보다 더 마주 볼 때만 막힌 것으로 본다. (면의 법선과 이동 방향의 내적, 0이면 스치는 면)
    private const float BlockingNormalDot = -0.05f;

    [Header("패턴")]
    [Tooltip("패턴(대시, 런)이 끝난 뒤 다음 패턴을 시전하기 전까지 Idle로 쉬는 시간입니다. 전투 시작 때의 Idle 시간은 '시작' 항목의 Start Idle Duration에서 따로 정합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _patternInterval = 1f;
    [Tooltip("Idle이 끝났을 때 대시를 고를 확률입니다. 0이면 항상 런, 1이면 항상 대시이고, 나머지 경우는 런입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _dashChance = 0.5f;

    [Header("몸 충돌 (대시·런 공통)")]
    [Tooltip("대시로 돌진하거나 런으로 달리는 동안의 몸 충돌 판정 범위입니다. 이 범위에 닿은 플레이어가 피해를 입습니다. 스컬독 그림 크기에 맞춘 값입니다.")]
    [SerializeField] private HitboxShape _bodyHitbox = new HitboxShape(Vector2.zero, new Vector2(3.6f, 1.4f));

    [Header("대시")]
    [Tooltip("대시 준비 자세(전조) 시간입니다. 준비를 시작하는 순간의 플레이어 위치를 기억해 두고, 이 시간이 지나면 그 자리로 돌진합니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _dashReadyDuration = 0.5f;
    [Tooltip("기억해 둔 플레이어 위치로 일직선 돌진하는 속도입니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _dashSpeed = 30f;
    [Tooltip("돌진이 목표 지점에 닿지 못하는 예외 상황에서 돌진을 끝내는 최대 시간입니다. (단위: 초)")]
    [SerializeField, Min(0.1f)] private float _dashMaxDuration = 1f;
    [Tooltip("돌진 중 몸에 닿은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 한 번의 대시에서 한 번만 맞습니다. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _dashDamageMultiplier = 1f;
    [Tooltip("대시가 끝났을 때 Idle로 쉬지 않고 이어서 한 번 더 대시할 확률입니다. 0이면 항상 Idle, 1이면 최대 횟수까지 항상 이어서 대시하고, 나머지 경우는 Idle입니다. 이어서 하는 대시는 끝난 자리(공중이면 공중)에서 중력 없이 떠 있는 채로 준비 자세부터 다시 합니다. 기획이 정해지기 전의 임시값입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _dashChainChance = 0.5f;
    [Tooltip("Idle 없이 연속으로 쓸 수 있는 대시의 최대 횟수입니다. 첫 대시를 포함합니다. 이 횟수만큼 대시하면 이어서 대시하지 않고 떨어져서 Idle로 갑니다. (단위: 번)")]
    [SerializeField, Min(1)] private int _maxDashChain = 3;
    [Tooltip("대시 속도선 효과의 회전 기준입니다. Visuals/MirroredVFX/Dash/DashLines를 연결합니다. 돌진하는 동안 돌진 방향으로 기울입니다. 켜고 끄는 것은 아래 두 피드백이 맡습니다.\n비워 두면 기울이지 않습니다.")]
    [SerializeField] private Transform _dashLines;
    [Tooltip("돌진을 시작할 때 속도선을 켜는 피드백입니다. Visuals/MirroredVFX/Dash/DashLines_Enter의 MMF_Player를 연결합니다.\n비워 두면 속도선이 나오지 않습니다.")]
    [SerializeField] private MMF_Player _dashLinesEnter;
    [Tooltip("돌진이 끝나거나 중간에 끊길 때 속도선을 끄는 피드백입니다. Visuals/MirroredVFX/Dash/DashLines_Exit의 MMF_Player를 연결합니다.")]
    [SerializeField] private MMF_Player _dashLinesExit;
    [Tooltip("돌진을 시작할 때 재생하는 소리입니다. 플레이어 대시와 같은 Assets/Data/Sound/Dash_Player를 씁니다. 분신이 돌진할 때도 이 소리를 씁니다.\n비워 두면 소리가 나지 않습니다.")]
    [SerializeField] private SoundDataSO _dashSound;

    [Header("런")]
    [Tooltip("런을 시작할 때의 속도입니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _runStartSpeed = 4f;
    [Tooltip("런을 하는 동안 1초마다 빨라지는 속도입니다. 클수록 빨리 최고 속도에 닿습니다. 방향을 바꿔도 처음 속도로 돌아가지 않고 계속 빨라집니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초²)")]
    [SerializeField, Min(0f)] private float _runAcceleration = 3f;
    [Tooltip("런의 최고 속도입니다. 이 속도에 닿으면 더 빨라지지 않습니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _runMaxSpeed = 16f;
    [Tooltip("관성의 세기입니다. 플레이어가 반대쪽으로 넘어갔을 때 1초에 속도를 이만큼 바꿉니다. 작을수록 오래 미끄러진 뒤에야 돌아서고, 클수록 바로 돌아섭니다. Run Acceleration보다 작으면 그 값으로 적용됩니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초²)")]
    [SerializeField, Min(0.1f)] private float _runTurnAcceleration = 20f;
    [Tooltip("달리는 동안 몸에 닿은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 플레이어의 피격 무적 시간이 끝난 뒤에도 닿아 있으면 다시 맞습니다. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _runDamageMultiplier = 1f;
    [Tooltip("런을 시작한 직후의 유예 시간입니다. 이 시간 동안에는 벽에 닿아도 스턴에 걸리지 않고(벽 쪽으로는 가지 못한 채 속도가 쌓이지 않습니다) 몸에 닿은 플레이어도 피해를 입지 않습니다. 시간이 지나면 벽 충돌과 몸 충돌 판정이 정상으로 들어갑니다. 벽에 붙은 채 런이 나왔을 때 바로 스턴에 걸리는 것을 막습니다. 0이면 유예 없이 시작합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _runGraceDuration = 0.5f;
    [Tooltip("런을 시작할 때 뒷발 먼지를 켜는 피드백입니다. Visuals/MirroredVFX/Run/RunDust_Enter의 MMF_Player를 연결합니다. (Mushroom 런과 같은 pixelFX impacts/run-dust) 폭주 런에도 나옵니다.\n비워 두면 먼지가 나오지 않습니다.")]
    [SerializeField] private MMF_Player _runDustEnter;
    [Tooltip("런이 끝나거나 중간에 끊길 때 뒷발 먼지를 끄는 피드백입니다. Visuals/MirroredVFX/Run/RunDust_Exit의 MMF_Player를 연결합니다.")]
    [SerializeField] private MMF_Player _runDustExit;

    [Header("패턴 게이지")]
    [Tooltip("패턴 게이지가 0%에서 100%까지 차는 데 걸리는 시간입니다. 기믹(하울링, 폭주 런) 중이 아니면 상태와 상관없이 일정하게 찹니다. 꽉 차면 게이지 UI가 푸른색으로 깜빡이고, 꽉 찬 채로 Idle이면 기믹(하울링)에 들어갑니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _gaugeFillDuration = 20f;
    [Tooltip("스컬독이 플레이어에게 피해를 입힐 때마다(대시, 런) 패턴 게이지가 차는 양입니다. 게이지 최대값이 100이면 퍼센트와 같습니다. 기믹 중에는 차지 않고, 분신이 입힌 피해로도 차지 않습니다.")]
    [SerializeField, Min(0f)] private float _hitGaugeGain = 10f;

    [Header("패턴 게이지 기믹 (하울링 · 폭주 런 · 분신)")]
    [Tooltip("기믹(폭주 런과 분신) 동안 패턴 게이지가 100%에서 0%까지 줄어드는 데 걸리는 시간입니다. 하울링이 끝난 순간부터 줄기 시작하고, 0%가 되면 기믹이 끝나 일반 런으로 이어집니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _gimmickDrainDuration = 20f;
    [Tooltip("기믹(폭주 런과 분신) 중에 플레이어가 스컬독을 때릴 때마다 패턴 게이지가 줄어드는 양입니다. 게이지 최대값이 100이면 퍼센트와 같습니다. 때릴수록 기믹이 빨리 끝납니다.")]
    [SerializeField, Min(0f)] private float _damagedGaugeLoss = 5f;
    [Tooltip("기믹 중에 나타나는 분신 프리팹입니다. Assets/Prefabs/FX/SkullDog/SkullDogClone을 연결합니다. 분신의 대시 속도·숨는 시간·피해 배율·색은 그 프리팹에서 조절합니다.\n비워 두면 분신 없이 폭주 런만 합니다.")]
    [SerializeField] private SkullDogClone _clonePrefab;

    [Header("스턴")]
    [Tooltip("런을 시작할 때의 속도(Run Start Speed) 이하로 벽에 부딪혔을 때의 스턴 시간입니다. 기획이 정해지기 전의 임시값입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _stunMinDuration = 1f;
    [Tooltip("런의 최고 속도(Run Max Speed)로 벽에 부딪혔을 때의 스턴 시간입니다. 그 사이의 속도는 속도에 비례해 Stun Min Duration과 이 값 사이로 정해집니다. 기획이 정해지기 전의 임시값입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _stunMaxDuration = 3f;
    [Tooltip("스턴하는 동안 머리 위(StatusVfxDisplay의 Anchor)에 띄울 VFX 프리팹입니다. 모든 보스가 함께 쓰는 Assets/Prefabs/FX/StatusVfx_Stun을 연결합니다. 반복 재생되는 Flipbook과 Manual 반환 PooledObject를 가진 프리팹을 씁니다.\n비워 두면 VFX 없이 스턴합니다.")]
    [SerializeField] private GameObject _stunVfxPrefab;
    [Tooltip("런으로 벽에 부딪혀 스턴에 걸릴 때 재생하는 피드백입니다. (카메라 흔들림, Knight 슬라이딩·BloodWing 대시 충돌과 같은 값) 루트 VFX/Impact/ImpactFeedbacks의 MMF_Player를 연결합니다.")]
    [SerializeField] private MMF_Player _impactFeedbacks;

    // 돌진을 멈추는 지형(바닥, 벽)을 찾는 조건
    private ContactFilter2D _obstacleFilter;
    private readonly RaycastHit2D[] _castResults = new RaycastHit2D[MaxCastCount];
    // Idle 없이 이어진 이번 연속 대시에서 지금까지 돌진한 횟수. Idle에서 대시를 고를 때 0으로 되돌린다.
    private int _dashChainCount;
    // 하울링 동안 Ghost 레이어로 바꿔 둔 상태인지와, 되돌릴 원래 레이어 (루트 몸 콜라이더, 피격 판정 콜라이더)
    private bool _isGhost;
    private int _ghostLayer;
    private int _bodyLayer;
    private int _hurtboxLayer;
    // 기믹 중에 쓰는 분신. 전투 시작 때 하나 만들어 꺼 두고, 기믹마다 다시 쓴다. 스컬독이 죽거나 Release되면 파괴한다.
    private SkullDogClone _clone;
    // 순간이동할 때 물리 위치를 바로 옮기는 데 쓴다.
    private Rigidbody2D _rigidbody;

    public SkullDogDashReadyState DashReadyState { get; private set; }
    public SkullDogDashState DashState { get; private set; }
    public SkullDogDashEndState DashEndState { get; private set; }
    public SkullDogRunState RunState { get; private set; }
    public SkullDogStunState StunState { get; private set; }
    public SkullDogHowlState HowlState { get; private set; }

    // 기믹(폭주 런과 분신)이 진행 중인지. 하울링이 끝난 순간부터 게이지가 0%가 되거나 기믹이 끊길 때까지 true다. (하울링 중에는 false)
    public bool IsGimmickActive { get; private set; }
    // 분신이 돌아다닐 수 있는 범위. 양쪽 벽·바닥의 안쪽 면과, 위쪽은 보스 패턴 게이지 바(UI)의 아래 가장자리다.
    // (UI를 찾지 못하면 천장 또는 화면 위쪽 끝) 전투 시작 때 한 번 계산한다. (카메라는 고정이다)
    public Vector2 FieldMin { get; private set; }
    public Vector2 FieldMax { get; private set; }

    public HitboxShape BodyHitbox => _bodyHitbox;
    public float DashReadyDuration => _dashReadyDuration;
    public float DashSpeed => _dashSpeed;
    public float DashMaxDuration => _dashMaxDuration;
    public float DashDamage => Stats.AttackPower * _dashDamageMultiplier;
    public float RunStartSpeed => _runStartSpeed;
    public float RunAcceleration => _runAcceleration;
    // 최고 속도를 시작 속도보다 작게 넣어도 시작 속도 밑으로 내려가지 않게 한다.
    public float RunMaxSpeed => Mathf.Max(_runStartSpeed, _runMaxSpeed);
    // 속도가 런의 가속을 따라가지 못하면 점점 빨라지지 않으므로 그보다 작아지지 않게 한다.
    public float RunTurnAcceleration => Mathf.Max(_runTurnAcceleration, _runAcceleration);
    public float RunDamage => Stats.AttackPower * _runDamageMultiplier;
    public float RunGraceDuration => _runGraceDuration;
    public GameObject StunVfxPrefab => _stunVfxPrefab;

    protected override bool OnInit(BattleContext context)
    {
        if (!base.OnInit(context))
        {
            return false;
        }

        _obstacleFilter = new ContactFilter2D();
        _obstacleFilter.SetLayerMask(LayerMask.GetMask(GroundLayerName, WallLayerName));
        _obstacleFilter.useTriggers = false;

        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.Initialization();
        }

        if (_stunVfxPrefab != null)
        {
            if (StatusVfx == null)
            {
                Debug.LogWarning("SkullDog 루트에 StatusVfxDisplay가 없어 스턴 VFX를 띄우지 않습니다.", this);
            }
            else if (PoolManager.HasInstance)
            {
                // 첫 스턴 때 생성 비용으로 끊기지 않도록 미리 하나 만들어 둔다.
                PoolManager.Instance.Prewarm(_stunVfxPrefab, 1);
            }
        }

        // 켜는 피드백은 초기화할 때 속도선을 꺼 둔다.
        if (_dashLinesEnter != null)
        {
            _dashLinesEnter.Initialization();
        }

        if (_dashLinesExit != null)
        {
            _dashLinesExit.Initialization();
        }

        if (_runDustEnter != null)
        {
            _runDustEnter.Initialization();
        }

        if (_runDustExit != null)
        {
            _runDustExit.Initialization();
        }

        _rigidbody = GetComponent<Rigidbody2D>();
        IsGimmickActive = false;
        _isGhost = false;
        _ghostLayer = LayerMask.NameToLayer(GhostLayerName);
        _bodyLayer = gameObject.layer;
        _hurtboxLayer = Hurtbox != null ? Hurtbox.gameObject.layer : gameObject.layer;

        if (_ghostLayer < 0)
        {
            Debug.LogError($"SkullDog: '{GhostLayerName}' 레이어가 없어 하울링 중에도 공격을 맞습니다.", this);
        }

        CalculateField();

        if (_clonePrefab == null)
        {
            Debug.LogWarning("SkullDog에 분신 프리팹이 없어 기믹 중 분신이 나오지 않습니다.", this);
        }
        else
        {
            // 첫 기믹 때 생성 비용으로 끊기지 않도록 미리 만들어 꺼 둔다.
            _clone = Instantiate(_clonePrefab);
            _clone.Initialize(this);
        }

        return true;
    }

    protected override void OnInitCompleted()
    {
        base.OnInitCompleted();
        Vital.Damaged += OnDamaged;
        Vital.Died += OnDiedInGimmick;
    }

    // 충돌 피드백(카메라 흔들림) 도중에 소환 해제되어도 흔들림이 남지 않게 멈춘다. 분신도 함께 없앤다.
    // 하울링 도중에 해제되면 상태의 Exit가 불리지 않으므로 Ghost 레이어도 여기서 되돌린다.
    protected override void OnRelease()
    {
        Vital.Damaged -= OnDamaged;
        Vital.Died -= OnDiedInGimmick;

        IsGimmickActive = false;
        DestroyClone();
        SetGhost(false);

        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.StopFeedbacks();
        }

        base.OnRelease();
    }

    // 패턴 게이지를 시간에 따라 채우거나 줄인다. 죽은 뒤에는 바뀌지 않는다.
    //  - 기믹(폭주 런과 분신) 중: Gimmick Drain Duration에 걸쳐 100%에서 0%까지 일정하게 준다. 0%가 되면 기믹이 끝난다.
    //  - 하울링 중: 그대로 둔다.
    //  - 그 외: Gauge Fill Duration에 걸쳐 0%에서 100%까지 일정하게 찬다. 꽉 차면 더 오르지 않는다.
    protected override void OnUpdate()
    {
        if (Vital.IsDead)
        {
            return;
        }

        if (IsGimmickActive)
        {
            PatternGauge.Reduce(PatternGauge.MaxValue / _gimmickDrainDuration * Time.deltaTime);

            // 시간으로 줄었든 플레이어에게 맞아 줄었든(OnDamaged) 0%가 되면 여기서 끝낸다.
            if (PatternGauge.CurrentValue <= 0f)
            {
                IsGimmickActive = false;
            }

            return;
        }

        if (CurrentState == HowlState)
        {
            return;
        }

        PatternGauge.Add(PatternGauge.MaxValue / _gaugeFillDuration * Time.deltaTime);
    }

    protected override void CreateStates()
    {
        base.CreateStates();
        DashReadyState = new SkullDogDashReadyState(this, stateMachine);
        DashState = new SkullDogDashState(this, stateMachine);
        DashEndState = new SkullDogDashEndState(this, stateMachine);
        RunState = new SkullDogRunState(this, stateMachine);
        StunState = new SkullDogStunState(this, stateMachine);
        HowlState = new SkullDogHowlState(this, stateMachine);
    }

    // Idle 중 매 프레임 호출된다. 패턴 게이지가 꽉 찼으면 Idle 시간을 기다리지 않고 바로 기믹(하울링)에 들어간다.
    // (Idle이 아닌 상태에서는 꽉 차도 들어가지 않고, 그 패턴이 끝나 Idle에 들어갈 때 들어간다)
    public override bool TryInterruptIdle()
    {
        if (!PatternGauge.IsFull)
        {
            return false;
        }

        stateMachine.ChangeState(HowlState);
        return true;
    }

    // 하울링이 끝났을 때 SkullDogHowlState가 호출한다. 기믹을 시작한다: 분신을 내보내고 폭주 런으로 들어간다.
    // 이때부터 게이지가 줄기 시작한다. (OnUpdate)
    public void BeginGimmick()
    {
        IsGimmickActive = true;

        if (_clone != null)
        {
            _clone.Begin();
        }

        stateMachine.ChangeState(RunState);
    }

    // 폭주 런이 중간에 끊겼을 때(플레이어 사망, SkullDog 사망 등) SkullDogRunState가 호출한다. 기믹을 그 자리에서 끝낸다.
    // 게이지를 비우고 분신을 바로 거둔다. 기믹 중이 아니면 아무것도 하지 않는다. (게이지가 0%가 되어 끝난 뒤의 일반 런 포함)
    public void CancelGimmick()
    {
        if (!IsGimmickActive)
        {
            return;
        }

        IsGimmickActive = false;
        PatternGauge.SetValue(0f);

        if (_clone != null)
        {
            _clone.Dismiss();
        }
    }

    // 스컬독이 플레이어에게 피해를 입혔을 때 대시·런 상태가 호출한다. 맞힌 수만큼 패턴 게이지를 채운다.
    // 기믹(하울링 포함) 중에는 채우지 않는다.
    public void AddGaugeForHits(int hitCount)
    {
        if (hitCount <= 0 || IsGimmickActive || CurrentState == HowlState)
        {
            return;
        }

        PatternGauge.Add(_hitGaugeGain * hitCount);
    }

    // 기믹에 들어갈 때 SkullDogHowlState가 호출한다. 화면 가로 가운데(양쪽 벽 사이 가운데)의 바닥 위로 순간이동한다.
    // 발(몸 콜라이더 아래쪽)이 바닥 면에 닿는 높이에 놓고, 움직임을 멈춘다.
    public void TeleportToFieldCenter()
    {
        Vector2 position = Movement.Position;
        float feetOffset = position.y - BodyCollider.bounds.min.y;
        Vector2 target = new Vector2((FieldMin.x + FieldMax.x) * 0.5f, FieldMin.y + feetOffset);

        _rigidbody.position = target;
        transform.position = new Vector3(target.x, target.y, transform.position.z);
        Movement.SetVelocity(Vector2.zero);
    }

    // 돌진을 시작할 때 재생한다. SkullDogDashState와 분신(SkullDogClone)이 호출한다.
    public void PlayDashSound()
    {
        if (_dashSound != null && SoundManager.HasInstance)
        {
            SoundManager.Instance.PlaySfx(_dashSound);
        }
    }

    // 런을 시작할 때 SkullDogRunState가 호출한다. 뒷발 먼지(Visuals/MirroredVFX/Run/RunDust)를 켠다. 보스 방향에 따라 같이 뒤집힌다.
    public void PlayRunDust()
    {
        if (_runDustEnter != null)
        {
            _runDustEnter.PlayFeedbacks();
        }
    }

    // 런이 끝나거나 끊길 때 SkullDogRunState가 호출한다. 뒷발 먼지를 끈다.
    public void StopRunDust()
    {
        if (_runDustExit != null)
        {
            _runDustExit.PlayFeedbacks();
        }
    }

    // 하울링하는 동안 몸 콜라이더(루트)와 피격 판정 콜라이더(자식 Collider)를 Ghost 레이어로 바꾸거나 원래 레이어로 되돌린다.
    // Ghost는 바닥 말고는 충돌하지 않고, 공격 판정은 Enemy 레이어만 조회하므로 플레이어의 공격이 통과한다. (GDD 6.2)
    public void SetGhost(bool isGhost)
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

    // 기믹 중에 플레이어에게 피해를 입으면 패턴 게이지가 줄어든다. 0%가 되면 OnUpdate가 기믹을 끝낸다.
    private void OnDamaged(DamageInfo damageInfo)
    {
        if (IsGimmickActive)
        {
            PatternGauge.Reduce(_damagedGaugeLoss);
        }
    }

    // 스컬독이 죽으면 분신을 바로 파괴한다. (기믹이 끝난 뒤 마지막 대시를 하던 분신도 포함)
    private void OnDiedInGimmick()
    {
        IsGimmickActive = false;
        DestroyClone();
    }

    private void DestroyClone()
    {
        if (_clone != null)
        {
            Destroy(_clone.gameObject);
            _clone = null;
        }
    }

    // 분신이 돌아다닐 범위를 계산한다. 메인 카메라가 비추는 화면에서, 화면 가운데를 기준으로 양쪽 벽·바닥·천장의 안쪽 면을 찾는다.
    // 찾지 못한 쪽은 화면 가장자리를 쓴다. (천장이 없는 씬 등)
    private void CalculateField()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogWarning($"{name}: 메인 카메라(MainCamera 태그)가 없어 분신이 스컬독 주변에만 나타납니다.", this);
            FieldMin = (Vector2)transform.position - Vector2.one;
            FieldMax = (Vector2)transform.position + Vector2.one;
            return;
        }

        Vector2 center = mainCamera.transform.position;
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        int surfaceMask = LayerMask.GetMask(GroundLayerName, WallLayerName);

        float left = FindSurface(center, Vector2.left, surfaceMask, center.x - halfWidth).x;
        float right = FindSurface(center, Vector2.right, surfaceMask, center.x + halfWidth).x;
        float bottom = FindSurface(center, Vector2.down, surfaceMask, center.y - halfHeight).y;
        float top = FindSurface(center, Vector2.up, surfaceMask, center.y + halfHeight).y;

        // 벽이 화면 밖에 있어도 화면 안에서만 나타나게 화면 가장자리로 자른다.
        FieldMin = new Vector2(Mathf.Max(left, center.x - halfWidth), Mathf.Max(bottom, center.y - halfHeight));
        FieldMax = new Vector2(Mathf.Min(right, center.x + halfWidth), Mathf.Min(top, center.y + halfHeight));

        // 위쪽은 보스 체력바 아래의 패턴 게이지 바에 가리지 않도록 그 아래 가장자리까지로 낮춘다.
        if (TryGetPatternGaugeBarBottom(mainCamera, out float barBottom))
        {
            FieldMax = new Vector2(FieldMax.x, Mathf.Min(FieldMax.y, barBottom));
        }
    }

    // 보스 패턴 게이지 바(UI)의 아래 가장자리가 가리키는 월드 높이를 구한다. 바를 찾지 못하면 false다.
    // 바의 네 모서리를 UI 카메라의 화면 좌표로 바꾼 뒤 메인 카메라의 월드 좌표로 바꿔, 가장 낮은 높이를 쓴다.
    // UI 배치(Canvas의 레이아웃)가 아직 계산되지 않았을 수 있어 먼저 갱신한다.
    private bool TryGetPatternGaugeBarBottom(Camera mainCamera, out float bottomY)
    {
        bottomY = 0f;
        PatternGaugeBar bar = FindFirstObjectByType<PatternGaugeBar>(FindObjectsInactive.Include);

        if (bar == null)
        {
            Debug.LogWarning($"{name}: 씬에서 PatternGaugeBar를 찾지 못해 분신이 화면 위쪽 끝까지 나타납니다. (보스 체력바에 가려질 수 있습니다)", this);
            return false;
        }

        Canvas canvas = bar.GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            return false;
        }

        canvas = canvas.rootCanvas;
        Canvas.ForceUpdateCanvases();

        // Screen Space - Overlay면 UI 카메라가 없다. (null이면 월드 좌표가 곧 화면 좌표)
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector3[] corners = new Vector3[4];
        ((RectTransform)bar.transform).GetWorldCorners(corners);
        bottomY = float.MaxValue;

        for (int i = 0; i < corners.Length; i++)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[i]);
            Vector3 worldPoint = mainCamera.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, Mathf.Abs(mainCamera.transform.position.z)));
            bottomY = Mathf.Min(bottomY, worldPoint.y);
        }

        return true;
    }

    // origin에서 direction(가로 또는 세로 단위 벡터)으로 가장 가까운 지형 면의 위치. 없으면 그 축의 값이 fallback인 위치를 반환한다.
    private static Vector2 FindSurface(Vector2 origin, Vector2 direction, int surfaceMask, float fallback)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, direction, FieldSearchDistance, surfaceMask);

        if (hit.collider != null)
        {
            return hit.point;
        }

        return direction.x != 0f ? new Vector2(fallback, origin.y) : new Vector2(origin.x, fallback);
    }

    // Idle 시간이 끝났을 때 호출된다. (플레이어가 죽어 있는 동안에는 호출되지 않는다)
    // 대시와 런 중 하나를 Dash Chance 확률로 고른다.
    public override void ChangeToNextPattern()
    {
        if (Random.value < _dashChance)
        {
            _dashChainCount = 0;
            stateMachine.ChangeState(DashReadyState);
        }
        else
        {
            stateMachine.ChangeState(RunState);
        }
    }

    // 패턴이 끝난 뒤 Idle로 쉬어간다. (Pattern Interval)
    public void ChangeToRest()
    {
        ChangeToIdle(_patternInterval);
    }

    // 런으로 벽에 부딪혔을 때 SkullDogRunState가 호출한다. 부딪힌 순간의 속도(impactSpeed)에 맞는 시간 동안 스턴에 걸린다.
    // 스턴 상태로 바꿨으면 true를 반환한다. 죽었거나 페이즈 전환 중이면 아무것도 하지 않는다.
    public bool ChangeToStun(float impactSpeed)
    {
        if (!IsInitialized || Vital.IsDead || IsPhaseTransitioning)
        {
            return false;
        }

        StunState.SetDuration(GetStunDuration(impactSpeed));
        stateMachine.ChangeState(StunState);
        return true;
    }

    // 벽에 부딪힌 속도에 맞는 스턴 시간. 런 시작 속도 이하면 최소, 최고 속도면 최대이고 그 사이는 속도에 비례한다.
    public float GetStunDuration(float impactSpeed)
    {
        float speedRatio = Mathf.InverseLerp(_runStartSpeed, RunMaxSpeed, impactSpeed);
        return Mathf.Lerp(_stunMinDuration, _stunMaxDuration, speedRatio);
    }

    // 대시가 향할 지점(루트 위치 기준). 지금 플레이어의 가로 위치이고, 높이는 스컬독의 발이 플레이어의 발 높이에 오는 자리다.
    // 플레이어가 바닥에 서 있으면 바닥을 따라 가로로 돌진하고, 공중에 있으면 그 높이까지 날아오른다.
    // 플레이어가 없으면(죽음) 지금 자리다.
    public Vector2 GetDashAimPoint()
    {
        Vector2 position = Movement.Position;

        if (IsTargetDead)
        {
            return position;
        }

        Bounds bodyBounds = BodyCollider.bounds;
        Bounds targetBounds = Target.BodyCollider.bounds;
        float aimX = targetBounds.center.x + (position.x - bodyBounds.center.x);
        float aimY = targetBounds.min.y + (position.y - bodyBounds.min.y);
        return new Vector2(aimX, aimY);
    }

    // 몸 콜라이더를 direction(단위 벡터)으로 distance만큼 옮길 때 벽·바닥에 막히는지 확인한다.
    // 막히면 true와 함께, 닿기 직전까지 옮길 수 있는 거리를 freeDistance로 준다. 안 막히면 freeDistance는 distance다.
    // 움직이는 방향을 마주 보는 면만 막힌 것으로 본다. (바닥을 따라 가로로 돌진할 때 발밑 바닥은 막는 것이 아니다)
    public bool CastBody(Vector2 direction, float distance, out float freeDistance)
    {
        Bounds bounds = BodyCollider.bounds;
        Vector2 castSize = new Vector2(Mathf.Max(0.01f, bounds.size.x - CastShrink * 2f), Mathf.Max(0.01f, bounds.size.y - CastShrink * 2f));
        int hitCount = Physics2D.BoxCast(bounds.center, castSize, 0f, direction, _obstacleFilter, _castResults, distance + CastShrink + CastContactSkin);
        float nearest = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            if (Vector2.Dot(_castResults[i].normal, direction) > BlockingNormalDot)
            {
                continue;
            }

            nearest = Mathf.Min(nearest, _castResults[i].distance);
        }

        if (nearest == float.MaxValue)
        {
            freeDistance = distance;
            return false;
        }

        freeDistance = Mathf.Clamp(nearest - CastShrink - CastContactSkin, 0f, distance);
        return true;
    }

    // 돌진을 시작할 때 SkullDogDashState가 호출한다. 이번 연속 대시에서 몇 번째 돌진인지 센다.
    public void CountDash()
    {
        _dashChainCount++;
    }

    // 대시가 끝났을 때 SkullDogDashEndState가 호출한다. Idle로 쉬지 않고 이어서 대시할지 Dash Chain Chance 확률로 고른다.
    // 이번 연속 대시에서 이미 Max Dash Chain번 돌진했으면 항상 false다.
    public bool RollDashChain()
    {
        return _dashChainCount < _maxDashChain && Random.value < _dashChainChance;
    }

    // 돌진을 시작할 때 SkullDogDashState가 호출한다. 속도선을 돌진 방향(direction, 단위 벡터)으로 기울이고 켠다.
    // 속도선은 좌우 반전을 맡는 Visuals 아래에 있으므로 보는 쪽(앞)을 +x로 둔 각도로 기울이면, 왼쪽을 볼 때는 부모의 반전이 맞춰 준다.
    // 돌진 중에는 보는 방향이 돌진 방향의 가로 부호와 같다. (수직 돌진은 어느 쪽을 보든 위·아래로 90도다)
    public void PlayDashLines(Vector2 direction)
    {
        if (_dashLines != null)
        {
            float angle = Mathf.Atan2(direction.y, Mathf.Abs(direction.x)) * Mathf.Rad2Deg;
            _dashLines.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (_dashLinesEnter != null)
        {
            _dashLinesEnter.PlayFeedbacks();
        }
    }

    // 돌진이 끝나거나 끊길 때 SkullDogDashState가 호출한다. 속도선을 끈다.
    public void StopDashLines()
    {
        if (_dashLinesExit != null)
        {
            _dashLinesExit.PlayFeedbacks();
        }
    }

    // 런으로 벽에 부딪혔을 때의 피드백(카메라 흔들림)을 재생한다.
    public void PlayImpactFeedbacks()
    {
        if (_impactFeedbacks != null)
        {
            _impactFeedbacks.PlayFeedbacks();
        }
    }

    protected override void DrawHitboxGizmos(float facingDirection)
    {
        DrawHitboxGizmo(_bodyHitbox, facingDirection, "대시·런 몸 충돌");
    }
}

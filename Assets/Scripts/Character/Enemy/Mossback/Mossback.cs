using System.Collections.Generic;
using UnityEngine;

// Mossback 보스. 공중형 보스다. 중력을 받지 않고 대기 높이(BloodWing의 대기 위치와 같은 높이)에 떠 있다.
// 소환되면 스폰 지점의 가로 위치에서 대기 높이로 바로 놓이고 Idle로 시작한다. (Enemy_GDD 10장)
// 흐름: Idle → Fly(대기 높이에서 좌우로 왔다 갔다) → 스매시 또는 샷 → Fly → ...
//  - Idle은 전투 시작과 스턴이 풀린 뒤에만 거친다. 스매시·샷이 끝나면 Idle 없이 바로 플라이로 돌아간다.
//  - 플라이: 최소 시간(Fly Min Duration)이 지나면 스매시와 샷 중 하나를 무작위로 고르고, 그 조건이 될 때까지 계속 왕복한다.
//  - 스매시: 플레이어가 수직 아래쯤에 오면 SmashStart(전조) → Smash(바닥까지 수직 낙하) → SmashEnd(일어남) → Rise(대기 높이로 복귀) → Fly.
//    떨어지는 동안 몸에 닿거나, 바닥에 닿은 순간 퍼지는 착지 충격(pixelFX heavy-landing) 범위에 닿은 플레이어가 피해를 입는다. (합쳐서 한 번만)
//  - 샷: 무작위로 고른 쪽(왼쪽/오른쪽) 왕복 끝에 닿으면 그 자리에 멈춰, 플레이어 쪽으로 부채꼴 발사체를 여러 번 쏜다. (MossbackShotState, MossbackProjectile)
//    발사체는 벽·땅·천장에 닿으면 벽돌깨기의 공처럼 튕긴다.
//  - 스턴: 상태와 애니메이션만 있고 조건이 정해지지 않아 패턴에 연결하지 않았다. 컴포넌트 컨텍스트 메뉴 "Stun (플레이 중)"으로 확인한다.
//    공중에서 스턴에 걸리면 중력으로 바닥에 떨어진 뒤 정해진 시간 동안 멈춰 있다가 대기 높이로 돌아가 Idle로 쉰다. (BloodWing과 같은 방식)
//  - 대기 높이·왕복 범위는 전투 시작 때 메인 카메라가 비추는 화면과 벽(Wall 레이어)을 기준으로 한 번 계산한다. (카메라는 고정이다)
// 아직 구현하지 않은 것: 2페이즈, 스턴 조건.
public class Mossback : Enemy
{
    private const string WallLayerName = "Wall";
    // 벽을 화면 가운데에서 찾는 최대 거리
    private const float WallSearchDistance = 100f;
    // 조준 방향을 정할 수 없다고 보는 최소 거리의 제곱 (발사 위치와 플레이어가 겹친 경우)
    private const float MinAimSqrDistance = 0.0001f;
    // 전투 시작 때 미리 만들어 두는 발사체 수. (한 번의 샷에 6개, 사라지기 전에 다음 샷이 나올 수 있어 두 번 분량)
    private const int ProjectilePrewarmCount = 12;

    [Header("공중 대기")]
    [Tooltip("화면 위쪽 가장자리에서 대기 높이(보스 중심)까지의 세로 거리입니다. 클수록 아래로 내려옵니다. BloodWing의 대기 위치(Perch Margin Y)와 같은 값으로 두면 같은 높이에 뜹니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _hoverMarginY = 3f;

    [Header("플라이")]
    [Tooltip("대기 높이에서 좌우로 왔다 갔다 하는 가로 속도입니다. 일정한 속도로 움직입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _flySpeed = 5f;
    [Tooltip("플라이 상태에 들어온 뒤 무조건 플라이로 머무는 시간입니다. 이 시간이 지나면 다음 패턴(스매시 또는 샷)을 고르고, 그 패턴의 조건이 될 때까지 계속 왕복합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _flyMinDuration = 5f;
    [Tooltip("양쪽 벽의 안쪽 면에서 왕복이 끝나는 지점(보스 중심)까지의 가로 거리입니다. 이 지점에 닿으면 방향을 바꿉니다. 샷은 이 지점에서 쏩니다. 그림의 반폭(약 1.3)보다 작으면 벽과 겹쳐 보입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _flyWallMargin = 2f;
    [Tooltip("플라이의 최소 시간이 지났을 때 다음 패턴으로 스매시를 고를 확률입니다. 0이면 항상 샷, 1이면 항상 스매시이고, 스매시를 고르지 않은 나머지는 샷입니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _smashChance = 0.5f;

    [Header("스매시")]
    [Tooltip("스매시를 고른 뒤, 플레이어와의 가로 거리가 이 값 이하가 되면 '수직 아래쯤에 왔다'고 보고 스매시를 시작합니다. 플레이어가 왕복 범위 바깥(벽 쪽)에 있으면 그쪽 왕복 끝을 기준으로 봅니다. (단위: 월드 거리)")]
    [SerializeField, Min(0.01f)] private float _smashTriggerRangeX = 0.5f;
    [Tooltip("스매시로 바닥을 향해 수직으로 떨어지는 속도입니다. 바닥에 닿을 때까지 이 속도로 떨어집니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _smashFallSpeed = 60f;
    [Tooltip("바닥에 닿지 않는 예외 상황에서 낙하를 끝내는 최대 시간입니다. 이 시간이 지나면 그 자리에서 대기 높이로 돌아갑니다. (단위: 초)")]
    [SerializeField, Min(0.1f)] private float _smashMaxDuration = 2f;
    [Tooltip("스매시(떨어지는 몸과 착지 충격)에 맞은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _smashDamageMultiplier = 1f;
    [Tooltip("떨어지는 동안의 몸 충돌 판정 범위입니다. 떨어지는 동안에만 이 범위에 닿은 플레이어가 피해를 입습니다. (한 번의 스매시에서 착지 충격과 합쳐 한 번만) 루트 위치 기준이며 오른쪽을 볼 때 기준입니다.")]
    [SerializeField] private HitboxShape _smashBodyHitbox = new HitboxShape(new Vector2(0f, 0.1f), new Vector2(2.2f, 3.2f));

    [Header("스매시 착지 충격")]
    [Tooltip("스매시로 바닥에 닿은 순간 발밑에 띄우는 효과 프리팹입니다. Assets/Prefabs/FX/HeavyLanding(pixelFX heavy-landing)을 연결합니다.\n비워 두면 효과는 나오지 않지만 착지 충격 판정은 그대로 나갑니다.")]
    [SerializeField] private GameObject _landingImpactPrefab;
    [Tooltip("착지 충격 판정이 살아 있는 시간입니다. 효과의 고리가 퍼지는 시간(0.33초)에 맞춰 두었습니다. 그 뒤에 남는 먼지에는 피해가 없습니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _landingImpactDuration = 0.33f;
    [Tooltip("착지 충격 판정의 처음 반폭입니다. 발밑에서 양쪽으로 이만큼 퍼진 상태에서 시작합니다. 효과의 첫 고리(반폭 22픽셀)에 맞춘 값입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _landingImpactStartHalfWidth = 1.4f;
    [Tooltip("착지 충격 판정의 마지막 반폭입니다. Landing Impact Duration 동안 처음 반폭에서 이 값까지 처음엔 빠르고 갈수록 느리게 퍼집니다. 효과의 고리가 가장 넓을 때(반폭 64픽셀 = 4)에 맞춘 값입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _landingImpactEndHalfWidth = 4f;
    [Tooltip("착지 충격 판정의 처음 높이입니다. 바닥에서 위쪽으로의 두께입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0.01f)] private float _landingImpactStartHeight = 0.25f;
    [Tooltip("착지 충격 판정의 마지막 높이입니다. 반폭과 같은 곡선으로 처음 높이에서 이 값까지 커집니다. 효과의 먼지 고리 높이(28픽셀 = 1.75)에 맞춘 값입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0.01f)] private float _landingImpactEndHeight = 1.75f;

    [Header("대기 높이 복귀")]
    [Tooltip("스매시가 끝나거나 스턴이 풀린 뒤 대기 높이까지 수직으로 돌아가는 속도입니다. 일정한 속도로 움직입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _riseSpeed = 8f;

    [Header("샷")]
    [Tooltip("샷으로 쏘는 발사체 프리팹입니다. MossbackProjectile과 Manual 반환 PooledObject를 가진 프리팹을 씁니다. (Assets/Prefabs/FX/Mossback/MossbackProjectile)\n비워 두면 샷 모션만 하고 아무것도 쏘지 않습니다.")]
    [SerializeField] private MossbackProjectile _projectilePrefab;
    [Tooltip("한 번의 샷에서 부채꼴 발사를 몇 번 하는지입니다.")]
    [SerializeField, Min(1)] private int _shotVolleyCount = 2;
    [Tooltip("부채꼴 발사 사이의 간격입니다. 마지막 발사 뒤 끝 모션으로 넘어가기 전에도 이만큼 기다립니다. 기획이 정해지기 전의 임시값입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _shotVolleyInterval = 0.5f;
    [Tooltip("부채꼴 발사 한 번에 쏘는 발사체 수입니다. 가운데 발사체가 플레이어를 향합니다. (짝수면 플레이어 방향이 두 발사체 사이가 됩니다)")]
    [SerializeField, Min(1)] private int _shotProjectileCount = 3;
    [Tooltip("부채꼴에서 이웃한 발사체 사이의 각도입니다. 3개에 20이면 플레이어 방향과 그 위아래 20도로 나갑니다. 기획이 정해지기 전의 임시값입니다. (단위: 도)")]
    [SerializeField, Range(0f, 90f)] private float _shotSpreadAngle = 20f;
    [Tooltip("발사체가 날아가는 속도입니다. 튕긴 뒤에도 같은 속도를 유지합니다. 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _shotProjectileSpeed = 9f;
    [Tooltip("발사체가 발사된 뒤 사라지기까지의 시간입니다. 튕긴 횟수와 상관없이 이 시간이 지나면 사라집니다. 플레이어에게 닿으면 그 전에 사라집니다. (단위: 초)")]
    [SerializeField, Min(0.1f)] private float _shotProjectileLifetime = 10f;
    [Tooltip("발사체에 맞은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = 스탯의 공격력 x 이 값. 기획이 정해지기 전의 임시값입니다.")]
    [SerializeField, Min(0f)] private float _shotDamageMultiplier = 1f;
    [Tooltip("발사체가 나오는 위치입니다. 루트 위치 기준이며 오른쪽을 볼 때 기준입니다. (왼쪽을 보면 x가 반대가 됩니다) 기획이 정해지기 전의 임시값입니다. (단위: 월드 거리)")]
    [SerializeField] private Vector2 _shotMuzzleOffset = new Vector2(0.6f, 0f);

    [Header("스턴")]
    [Tooltip("스턴에 걸렸을 때 멈춰 있는 시간입니다. 공중에서 걸리면 바닥에 닿은 순간부터 셉니다. 끝나면 대기 높이로 돌아갑니다. 스턴 조건은 아직 정해지지 않아 패턴에 연결되어 있지 않습니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _stunDuration = 3f;
    [Tooltip("스턴이 풀려 대기 높이로 돌아온 뒤 Idle로 쉬는 시간입니다. 이 시간이 지나면 플라이로 넘어갑니다. 전투 시작 때의 Idle 시간은 '시작' 항목의 Start Idle Duration에서 따로 정합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _stunRecoverIdleDuration = 2f;
    [Tooltip("스턴 동안 머리 위(StatusVfxDisplay의 Anchor)에 띄울 VFX 프리팹입니다. 반복 재생되는 Flipbook과 Manual 반환 PooledObject를 가진 프리팹을 씁니다.\n비워 두면 VFX 없이 스턴에 걸립니다.")]
    [SerializeField] private GameObject _stunVfxPrefab;

    // 지금까지 쏜 발사체 중 아직 날아다니는 것들. Mossback이 죽거나 Release될 때 한꺼번에 없애려고 기록한다.
    private readonly List<MossbackProjectile> _projectiles = new List<MossbackProjectile>();

    // 전투 시작 때 계산해 두는 값들. (월드 좌표)
    private float _hoverY;
    // 화면 가운데의 x. 플라이를 시작할 때 어느 쪽으로 먼저 갈지 정하는 기준이다.
    private float _fieldCenterX;
    // 플라이로 왕복하는 가로 범위의 양 끝 (보스 중심 기준)
    private float _flyMinX;
    private float _flyMaxX;

    // 스매시 착지 충격 판정이 살아 있는지와 그 자리. (StartLandingImpact / UpdateLandingImpact)
    private bool _isLandingImpactActive;
    private Vector2 _landingImpactOrigin;
    private float _landingImpactStartTime;

    public MossbackFlyState FlyState { get; private set; }
    public MossbackSmashStartState SmashStartState { get; private set; }
    public MossbackSmashState SmashState { get; private set; }
    public MossbackSmashEndState SmashEndState { get; private set; }
    public MossbackRiseState RiseState { get; private set; }
    public MossbackShotState ShotState { get; private set; }
    public MossbackStunState StunState { get; private set; }

    public float HoverY => _hoverY;
    public float FieldCenterX => _fieldCenterX;
    public float FlyMinX => _flyMinX;
    public float FlyMaxX => _flyMaxX;
    public float FlySpeed => _flySpeed;
    public float FlyMinDuration => _flyMinDuration;
    public float SmashChance => _smashChance;
    public float SmashFallSpeed => _smashFallSpeed;
    public float SmashMaxDuration => _smashMaxDuration;
    public float SmashDamage => Stats.AttackPower * _smashDamageMultiplier;
    public HitboxShape SmashBodyHitbox => _smashBodyHitbox;
    public float RiseSpeed => _riseSpeed;
    public int ShotVolleyCount => _shotVolleyCount;
    public float ShotVolleyInterval => _shotVolleyInterval;
    public float StunDuration => _stunDuration;
    public float StunRecoverIdleDuration => _stunRecoverIdleDuration;
    public GameObject StunVfxPrefab => _stunVfxPrefab;

    // 스매시를 시작해도 되는 자리인지. 플레이어가 수직 아래쯤(가로 거리 Smash Trigger Range X 이하)에 있으면 true다.
    // 플레이어가 왕복 범위 바깥(벽 쪽)에 있으면 더 다가갈 수 없으므로 그쪽 왕복 끝에 닿았을 때 true가 된다.
    public bool IsOverTarget
    {
        get
        {
            if (IsTargetDead)
            {
                return false;
            }

            float targetX = Mathf.Clamp(Target.transform.position.x, _flyMinX, _flyMaxX);
            return Mathf.Abs(targetX - Movement.Position.x) <= _smashTriggerRangeX;
        }
    }

    protected override bool OnInit(BattleContext context)
    {
        if (!base.OnInit(context))
        {
            return false;
        }

        if (!TryCalculateFlightRange())
        {
            return false;
        }

        _isLandingImpactActive = false;

        if (_projectilePrefab == null)
        {
            Debug.LogWarning("Mossback에 발사체 프리팹(Projectile Prefab)이 없어 샷에서 아무것도 쏘지 않습니다.", this);
        }

        if (PoolManager.HasInstance)
        {
            // 첫 사용 때 생성 비용으로 끊기지 않도록 미리 만들어 둔다.
            if (_projectilePrefab != null)
            {
                PoolManager.Instance.Prewarm(_projectilePrefab.gameObject, ProjectilePrewarmCount);
            }

            if (_landingImpactPrefab != null)
            {
                PoolManager.Instance.Prewarm(_landingImpactPrefab, 1);
            }
        }

        if (_stunVfxPrefab != null)
        {
            if (StatusVfx == null)
            {
                Debug.LogWarning("Mossback 루트에 StatusVfxDisplay가 없어 스턴 VFX를 띄우지 않습니다.", this);
            }
            else if (PoolManager.HasInstance)
            {
                PoolManager.Instance.Prewarm(_stunVfxPrefab, 1);
            }
        }

        return true;
    }

    // 전투가 시작될 때 중력을 끄고, 스폰 지점의 가로 위치(왕복 범위 안)에서 대기 높이에 바로 놓은 뒤 화면 안쪽을 본다.
    // 중력은 Release에서 되돌리지 않는다. (Release는 파괴 중에도 호출되어 Rigidbody2D가 이미 없을 수 있고, 다시 Init하면 여기서 다시 끈다)
    protected override void OnInitCompleted()
    {
        base.OnInitCompleted();
        Vital.Died += OnMossbackDied;

        Movement.SetGravityEnabled(false);
        Movement.SetVelocity(Vector2.zero);

        float startX = Mathf.Clamp(transform.position.x, _flyMinX, _flyMaxX);
        transform.position = new Vector3(startX, _hoverY, transform.position.z);
        Animation.UpdateFacing(_fieldCenterX - startX);
    }

    protected override void OnRelease()
    {
        Vital.Died -= OnMossbackDied;
        _isLandingImpactActive = false;
        ClearProjectiles();
        base.OnRelease();
    }

    protected override void CreateStates()
    {
        base.CreateStates();
        FlyState = new MossbackFlyState(this, stateMachine);
        SmashStartState = new MossbackSmashStartState(this, stateMachine);
        SmashState = new MossbackSmashState(this, stateMachine);
        SmashEndState = new MossbackSmashEndState(this, stateMachine);
        RiseState = new MossbackRiseState(this, stateMachine);
        ShotState = new MossbackShotState(this, stateMachine);
        StunState = new MossbackStunState(this, stateMachine);
    }

    protected override void OnUpdate()
    {
        UpdateLandingImpact();
    }

    // Idle 시간이 끝났을 때 호출된다. (플레이어가 죽어 있는 동안에는 호출되지 않는다)
    // Idle(전투 시작, 스턴이 풀린 뒤) 다음은 항상 플라이다. 스매시와 샷은 플라이가 고른다.
    public override void ChangeToNextPattern()
    {
        stateMachine.ChangeState(FlyState);
    }

    // 대기 높이로 돌아가는 상태(Rise)로 바꾼다. endsWithIdle이 true면 도착한 뒤 Idle로 쉬고(스턴이 풀린 뒤), false면 바로 플라이로 넘어간다. (스매시 뒤)
    public void ChangeToRise(bool endsWithIdle)
    {
        RiseState.SetEndsWithIdle(endsWithIdle);
        stateMachine.ChangeState(RiseState);
    }

    // 스매시로 바닥에 닿은 순간 MossbackSmashState가 호출한다. 발밑에 착지 효과(pixelFX heavy-landing)를 띄우고
    // 같은 범위의 판정을 Landing Impact Duration 동안 낸다. (UpdateLandingImpact) 효과와 판정은 Mossback을 따라가지 않고 그 자리에 남는다.
    public void StartLandingImpact()
    {
        Bounds bodyBounds = BodyCollider.bounds;
        _landingImpactOrigin = new Vector2(bodyBounds.center.x, bodyBounds.min.y);
        _landingImpactStartTime = Time.time;
        _isLandingImpactActive = true;

        if (_landingImpactPrefab != null && PoolManager.HasInstance)
        {
            PoolManager.Instance.Spawn(_landingImpactPrefab, _landingImpactOrigin, Quaternion.identity);
        }
    }

    // 플레이어 쪽으로 부채꼴 발사를 한 번 한다. MossbackShotState가 발사할 때마다 호출한다.
    // 가운데 방향은 발사 위치에서 플레이어 몸 중심을 향하고, 발사체들은 그 방향을 기준으로 Shot Spread Angle 간격으로 위아래에 퍼진다.
    // 플레이어가 없거나 죽었으면 보는 방향으로 수평하게 쏜다.
    public void FireVolley()
    {
        if (_projectilePrefab == null || !PoolManager.HasInstance)
        {
            return;
        }

        RemoveFinishedProjectiles();

        float facing = Animation.FacingDirection;
        Vector2 muzzle = (Vector2)transform.position + new Vector2(_shotMuzzleOffset.x * facing, _shotMuzzleOffset.y);
        Vector2 aim = new Vector2(facing, 0f);

        if (!IsTargetDead)
        {
            Vector2 toTarget = (Vector2)Target.BodyCollider.bounds.center - muzzle;

            if (toTarget.sqrMagnitude > MinAimSqrDistance)
            {
                aim = toTarget;
            }
        }

        float aimAngle = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;
        float damage = Stats.AttackPower * _shotDamageMultiplier;

        for (int i = 0; i < _shotProjectileCount; i++)
        {
            float angle = (aimAngle + (i - (_shotProjectileCount - 1) * 0.5f) * _shotSpreadAngle) * Mathf.Deg2Rad;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            GameObject instance = PoolManager.Instance.Spawn(_projectilePrefab.gameObject, muzzle, Quaternion.identity);
            MossbackProjectile projectile = instance.GetComponent<MossbackProjectile>();

            if (projectile == null)
            {
                Debug.LogError("발사체 프리팹에 MossbackProjectile이 없습니다.", this);
                PoolManager.Instance.Despawn(instance);
                return;
            }

            projectile.Launch(muzzle, direction, _shotProjectileSpeed, _shotProjectileLifetime, damage, gameObject);
            _projectiles.Add(projectile);
        }
    }

    // 스턴 상태로 바꾼다. 죽었거나 페이즈 전환 중이면 아무것도 하지 않는다.
    // 플레이 중 컴포넌트 컨텍스트 메뉴로 스턴을 확인한다.
    [ContextMenu("Stun (플레이 중)")]
    private void PreviewStun()
    {
        ChangeToStun();
    }

    // 스턴 조건이 정해지면 그 조건을 확인하는 쪽에서 호출한다. 스턴 상태로 바꿨으면 true를 반환한다.
    public bool ChangeToStun()
    {
        if (!IsInitialized || Vital.IsDead || IsPhaseTransitioning)
        {
            return false;
        }

        stateMachine.ChangeState(StunState);
        return true;
    }

    protected override void DrawHitboxGizmos(float facingDirection)
    {
        DrawHitboxGizmo(_smashBodyHitbox, facingDirection, "스매시 몸 충돌");

        // 착지 충격이 가장 넓게 퍼졌을 때의 범위. 발밑(몸 콜라이더 아래쪽 끝)에서 위로 솟는다.
        if (TryGetComponent(out Collider2D bodyCollider))
        {
            Bounds bodyBounds = bodyCollider.bounds;
            Vector2 center = new Vector2(bodyBounds.center.x, bodyBounds.min.y + _landingImpactEndHeight * 0.5f);
            DrawHitboxGizmo(center, new Vector2(_landingImpactEndHalfWidth * 2f, _landingImpactEndHeight), 0f, "스매시 착지 충격 (최대)");
        }
    }

    // 착지 충격 판정을 낸다. 발밑에서 양쪽으로 반폭이, 위로 높이가 처음엔 빠르고 갈수록 느리게(ease-out) 커진다. (효과의 고리가 퍼지는 모양에 맞춤)
    // 스매시 몸통과 같은 판정 기록을 쓰므로, 이번 스매시에서 몸통에 이미 맞은 플레이어는 착지 충격에 다시 맞지 않는다. (한 번만 맞는다)
    private void UpdateLandingImpact()
    {
        if (!_isLandingImpactActive)
        {
            return;
        }

        float elapsedTime = Time.time - _landingImpactStartTime;

        if (Vital.IsDead || elapsedTime > _landingImpactDuration)
        {
            _isLandingImpactActive = false;
            return;
        }

        float progress = elapsedTime / _landingImpactDuration;
        float eased = 1f - (1f - progress) * (1f - progress);
        float halfWidth = Mathf.Lerp(_landingImpactStartHalfWidth, _landingImpactEndHalfWidth, eased);
        float height = Mathf.Lerp(_landingImpactStartHeight, _landingImpactEndHeight, eased);
        Vector2 center = _landingImpactOrigin + new Vector2(0f, height * 0.5f);

        HitChecker.Hit(center, new Vector2(halfWidth * 2f, height), 0f, SmashDamage);
    }

    // Mossback이 죽으면 날아다니던 발사체를 모두 없앤다. (죽은 뒤에 플레이어가 맞지 않게 한다)
    private void OnMossbackDied()
    {
        ClearProjectiles();
    }

    // 이미 사라진(풀로 돌아간) 발사체를 기록에서 뺀다. 풀에서 다시 꺼낸 발사체가 기록에 두 번 들어가지 않도록 새로 쏘기 전에 호출한다.
    private void RemoveFinishedProjectiles()
    {
        for (int i = _projectiles.Count - 1; i >= 0; i--)
        {
            if (_projectiles[i] == null || !_projectiles[i].IsFlying)
            {
                _projectiles.RemoveAt(i);
            }
        }
    }

    private void ClearProjectiles()
    {
        for (int i = 0; i < _projectiles.Count; i++)
        {
            // 씬이 닫히는 중에는 발사체가 먼저 파괴되어 있을 수 있다.
            if (_projectiles[i] != null)
            {
                _projectiles[i].Finish();
            }
        }

        _projectiles.Clear();
    }

    // origin에서 direction(왼쪽 -1, 오른쪽 1)으로 가장 가까운 벽(Wall 레이어)의 안쪽 면 x. 벽이 없으면 fallbackX를 반환한다.
    private float FindWallInnerX(Vector2 origin, float direction, float fallbackX)
    {
        RaycastHit2D hit = Physics2D.Raycast(origin, new Vector2(direction, 0f), WallSearchDistance, LayerMask.GetMask(WallLayerName));
        return hit.collider != null ? hit.point.x : fallbackX;
    }

    // 메인 카메라가 비추는 화면과 양쪽 벽을 기준으로 대기 높이와 플라이 왕복 범위를 계산한다.
    // 기준을 찾지 못하면 false를 반환한다. (Init 실패)
    private bool TryCalculateFlightRange()
    {
        Camera mainCamera = Camera.main;

        if (mainCamera == null)
        {
            Debug.LogError($"{name}: 메인 카메라(MainCamera 태그)가 없어 대기 높이를 정할 수 없습니다.", this);
            return false;
        }

        Vector2 cameraCenter = mainCamera.transform.position;
        float halfHeight = mainCamera.orthographicSize;
        float halfWidth = halfHeight * mainCamera.aspect;
        float leftWallX = FindWallInnerX(cameraCenter, -1f, cameraCenter.x - halfWidth);
        float rightWallX = FindWallInnerX(cameraCenter, 1f, cameraCenter.x + halfWidth);

        _fieldCenterX = cameraCenter.x;
        _hoverY = cameraCenter.y + halfHeight - _hoverMarginY;
        // 벽 사이가 좁아 범위가 뒤집히면 가운데 한 점으로 모은다.
        _flyMinX = Mathf.Min(leftWallX + _flyWallMargin, _fieldCenterX);
        _flyMaxX = Mathf.Max(rightWallX - _flyWallMargin, _fieldCenterX);
        return true;
    }
}

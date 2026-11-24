using UnityEngine;

// 결석: 보스를 처치하면 보스 몸에서 튀어나오는 보상 물체. (GDD 6.6) 기본 동작·액티브 룬·패시브 룬 중 하나를 준다.
// 움직임: 켜지는 순간 위로 솟아오르고 → 중력으로 떨어지고 → 바닥·벽에서 탱탱볼처럼 튕기다가 → 튀는 힘이 약해지면 멈춘다.
//  - 물리 엔진의 충돌에 맡기지 않고 직접 계산한다. (Kinematic) 결석이 쓰는 Interactable 레이어는 바닥·벽과 충돌하지 않게 설정되어 있어,
//    매 물리 프레임 이동 경로에 지형(Ground, Wall)이 있는지 원 모양으로 검사해 닿은 면에서 튕긴다. 벽·천장에서도 튕기므로 필드 밖으로 나가지 않는다.
// 줍기: 처음 바닥에 닿은 뒤부터, 플레이어의 피격 판정(Player 레이어 트리거)이 닿으면 RewardStoneCollectedEvent를 발행하고 사라진다.
//  - 솟아오르는 동안에는 닿아도 줍지 않는다. (보스 바로 옆에서 잡아도 튀어 오르는 모습이 보이도록)
//  - 실제 획득 처리(동작 해금, 룬 지급)는 아직 없다. 지금은 획득 창을 여는 이벤트만 발행한다.
// 프리팹 구성: 루트(Interactable 레이어)에 이 컴포넌트, Rigidbody2D(Kinematic), CircleCollider2D(트리거). 그림은 자식 Visual이 맡는다.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class RewardStone : MonoBehaviour
{
    private const string GroundLayerName = "Ground";
    private const string WallLayerName = "Wall";
    private const string PlayerHurtboxLayerName = "Player";
    // 지형에 닿은 뒤 겹치지 않도록 면에서 띄우는 거리 (단위: 월드 거리)
    private const float SkinWidth = 0.01f;
    // 한 물리 프레임에서 처리하는 최대 튕김 횟수. (구석에서 바닥과 벽에 연달아 닿는 경우)
    private const int MaxBouncesPerStep = 3;
    // 닿은 면의 법선 y가 이 값보다 크면 바닥으로 본다.
    private const float FloorNormalThreshold = 0.5f;
    // 한 번의 검사에서 받는 지형 수
    private const int HitBufferSize = 8;

    [Header("보상")]
    [Tooltip("이 결석이 주는 보상의 종류입니다. 획득 창의 종류 팻말(기본 동작 / 액티브 룬 / 패시브 룬)이 이 값으로 정해집니다.")]
    [SerializeField] private RewardType _rewardType;
    [Tooltip("획득 창에 표시할 아이콘입니다. 비워 두면 회색 사각형으로 표시됩니다.")]
    [SerializeField] private Sprite _icon;
    [Tooltip("획득 창의 설명창에 표시할 글입니다. 한글 TMP 폰트가 생기기 전까지는 영문으로 적습니다.")]
    [SerializeField, TextArea(2, 5)] private string _description;

    [Header("솟아오르기")]
    [Tooltip("나오는 순간 위로 솟아오르는 속도입니다. 클수록 높이 올라갑니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _launchSpeed = 14f;
    [Tooltip("나오는 순간 좌우로 퍼지는 속도의 최댓값입니다. 0부터 이 값 사이에서 무작위로 정해지고 방향(왼쪽/오른쪽)도 무작위입니다. 0이면 똑바로 위로만 솟습니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _launchHorizontalSpeed = 1.5f;

    [Header("낙하·튕김")]
    [Tooltip("아래로 당기는 중력의 세기입니다. 클수록 빨리 떨어집니다. (단위: 월드 거리/초²)")]
    [SerializeField, Min(0f)] private float _gravity = 35f;
    [Tooltip("떨어지는 속도의 최댓값입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _maxFallSpeed = 25f;
    [Tooltip("지형에 닿았을 때 튕겨 나오는 세기입니다. 닿기 직전 속도에 이 비율을 곱한 속도로 튕깁니다. 0이면 튕기지 않고 1이면 같은 속도로 튕깁니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _bounciness = 0.6f;
    [Tooltip("바닥에서 튕길 때마다 남는 가로 속도의 비율입니다. 0이면 바닥에 닿는 순간 가로로 멈추고 1이면 줄지 않습니다. (0~1)")]
    [SerializeField, Range(0f, 1f)] private float _floorHorizontalKeep = 0.7f;
    [Tooltip("바닥에서 튕겨 오르는 속도가 이 값보다 작아지면 튕기지 않고 그 자리에 멈춥니다. 클수록 일찍 멈춥니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _restSpeed = 2f;

    private readonly RaycastHit2D[] _hits = new RaycastHit2D[HitBufferSize];

    private Rigidbody2D _rigidbody;
    private CircleCollider2D _collider;
    private ContactFilter2D _terrainFilter;
    private int _playerHurtboxLayer;
    private Vector2 _velocity;
    // 처음 바닥에 닿았는지 여부. 닿은 뒤부터 주울 수 있다.
    private bool _hasLanded;
    // 튕김이 끝나 바닥에 멈췄는지 여부
    private bool _isResting;
    private bool _isCollected;

    public RewardType RewardType => _rewardType;
    // 지금 플레이어가 닿으면 주울 수 있는지 여부
    public bool CanBeCollected => _hasLanded && !_isCollected;
    public bool IsResting => _isResting;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<CircleCollider2D>();

        // 움직임은 이 컴포넌트가 직접 계산한다. 멈춘 뒤에도 플레이어가 닿는 것을 놓치지 않도록 잠들지 않게 한다.
        _rigidbody.bodyType = RigidbodyType2D.Kinematic;
        _rigidbody.sleepMode = RigidbodySleepMode2D.NeverSleep;
        _collider.isTrigger = true;

        _terrainFilter = new ContactFilter2D();
        _terrainFilter.SetLayerMask(LayerMask.GetMask(GroundLayerName, WallLayerName));
        _terrainFilter.useTriggers = false;
        _playerHurtboxLayer = LayerMask.NameToLayer(PlayerHurtboxLayerName);
    }

    private void OnEnable()
    {
        Launch();
    }

    // 지금 위치에서 위로 솟아오르기 시작한다. 켜질 때 자동으로 호출된다.
    public void Launch()
    {
        float horizontalSpeed = Random.Range(-_launchHorizontalSpeed, _launchHorizontalSpeed);
        _velocity = new Vector2(horizontalSpeed, _launchSpeed);
        _hasLanded = false;
        _isResting = false;
        _isCollected = false;
    }

    private void FixedUpdate()
    {
        if (_isResting || _isCollected)
        {
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        _velocity.y = Mathf.Max(_velocity.y - _gravity * deltaTime, -_maxFallSpeed);

        Vector2 position = _rigidbody.position;
        float radius = GetWorldRadius();
        float remainingTime = deltaTime;

        for (int i = 0; i < MaxBouncesPerStep && remainingTime > 0f && !_isResting; i++)
        {
            Vector2 move = _velocity * remainingTime;
            float distance = move.magnitude;

            if (distance <= Mathf.Epsilon)
            {
                break;
            }

            if (!TryFindNearestTerrain(position, radius, move / distance, distance, out RaycastHit2D hit))
            {
                position += move;
                break;
            }

            // 닿은 자리까지만 가고, 남은 시간은 튕긴 방향으로 마저 움직인다.
            position = hit.centroid + hit.normal * SkinWidth;
            remainingTime *= 1f - Mathf.Clamp01(hit.distance / distance);
            Bounce(hit.normal);
        }

        _rigidbody.MovePosition(position);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other);
    }

    // 플레이어와 겹친 채로 처음 바닥에 닿는 경우, 닿는 순간 Enter가 다시 오지 않으므로 겹쳐 있는 동안에도 확인한다.
    private void OnTriggerStay2D(Collider2D other)
    {
        TryCollect(other);
    }

    // 이동 경로에서 가장 먼저 닿는 지형을 찾는다. 없으면 false를 반환한다.
    private bool TryFindNearestTerrain(Vector2 origin, float radius, Vector2 direction, float distance, out RaycastHit2D nearestHit)
    {
        int hitCount = Physics2D.CircleCast(origin, radius, direction, _terrainFilter, _hits, distance);
        nearestHit = default;
        bool hasHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = _hits[i];

            if (hit.distance <= 0f && !TryResolveTouchingTerrain(origin, direction, ref hit))
            {
                continue;
            }

            if (!hasHit || hit.distance < nearestHit.distance)
            {
                nearestHit = hit;
                hasHit = true;
            }
        }

        return hasHit;
    }

    // 이미 지형과 맞닿았거나 겹친 채로 움직이기 시작한 경우(거리 0)를 처리한다. (몸이 낮은 보스의 몸 가운데에서 나와 바닥에 맞닿아 있을 때 등)
    // 이때 검사가 알려 주는 법선은 믿을 수 없으므로, 두 콜라이더 사이의 실제 분리 방향으로 바꾼다.
    // 지형에서 멀어지는 중이면 닿은 것으로 치지 않고(false), 지형 쪽으로 가는 중이면 겹친 만큼 밀어낸 자리에서 튕기게 한다.
    private bool TryResolveTouchingTerrain(Vector2 origin, Vector2 direction, ref RaycastHit2D hit)
    {
        ColliderDistance2D separation = hit.collider.Distance(_collider);

        if (!separation.isValid)
        {
            return true;
        }

        // 결석이 지형에서 벗어나는 방향. (떨어져 있으면 normal이 지형 쪽을, 겹쳐 있으면 벗어나는 쪽을 가리킨다)
        Vector2 outward = separation.isOverlapped ? separation.normal : -separation.normal;

        if (Vector2.Dot(direction, outward) >= 0f)
        {
            return false;
        }

        hit.normal = outward;
        hit.centroid = separation.isOverlapped ? origin + (separation.pointA - separation.pointB) : origin;
        return true;
    }

    // 닿은 면에서 튕긴다. 면을 향하던 속도는 튕김 비율만큼 줄여 반대로 뒤집고, 면을 따라가던 속도는 유지한다.
    private void Bounce(Vector2 normal)
    {
        float normalSpeed = Vector2.Dot(_velocity, normal);

        // 이미 면에서 멀어지는 중이면 튕기지 않는다.
        if (normalSpeed >= 0f)
        {
            return;
        }

        Vector2 normalVelocity = normal * normalSpeed;
        Vector2 tangentVelocity = _velocity - normalVelocity;

        if (normal.y > FloorNormalThreshold)
        {
            _hasLanded = true;

            if (-normalSpeed * _bounciness < _restSpeed)
            {
                _velocity = Vector2.zero;
                _isResting = true;
                return;
            }

            tangentVelocity *= _floorHorizontalKeep;
        }

        _velocity = tangentVelocity - normalVelocity * _bounciness;
    }

    private void TryCollect(Collider2D other)
    {
        if (!CanBeCollected || other.gameObject.layer != _playerHurtboxLayer)
        {
            return;
        }

        if (other.GetComponentInParent<Player>() == null)
        {
            return;
        }

        _isCollected = true;

        // 실제 획득 처리(동작 해금, 룬 지급)는 아직 없다. 지금은 획득 창을 여는 이벤트만 발행한다.
        EventBus<RewardStoneCollectedEvent>.Publish(new RewardStoneCollectedEvent(_rewardType, _icon, _description));
        Destroy(gameObject);
    }

    // 판정 원의 월드 반지름. 프리팹의 스케일을 반영한다.
    private float GetWorldRadius()
    {
        Vector3 scale = transform.lossyScale;
        return _collider.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
    }
}

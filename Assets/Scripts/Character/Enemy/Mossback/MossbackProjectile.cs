using UnityEngine;

// Mossback 샷의 발사체 하나. 일정한 속도로 곧게 날아가고, 벽·땅·천장(Ground, Wall 레이어)에 닿으면 벽돌깨기의 공처럼 튕긴다. (Enemy_GDD 10장)
// 튕길 때는 들어온 각도와 같은 각도로 반사되며(Vector2.Reflect) 속도는 그대로다. 중력은 받지 않는다.
// Launch로 시작한다. 몸(원)에 닿은 플레이어는 피해를 입고, 발사체는 그 자리에서 사라진다. 닿지 않아도 수명(lifetime)이 지나면 사라진다.
// 그림은 그레이박스(원)이며 색은 프리팹의 SpriteRenderer에서 정한다.
// 풀(PoolManager)에서 꺼내 쓰므로 PooledObject의 반환 방식은 Manual로 둔다. 움직임은 Kinematic Rigidbody2D로 물리 프레임에서 옮긴다.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class MossbackProjectile : MonoBehaviour
{
    private const string PlayerLayerName = "Player";
    private const string GroundLayerName = "Ground";
    private const string WallLayerName = "Wall";
    // 한 번의 판정에서 처리할 최대 콜라이더 수
    private const int MaxOverlapCount = 4;
    // 한 물리 프레임에 처리할 최대 튕김 횟수. (모서리에서 두 면에 잇달아 닿는 경우)
    private const int MaxBouncesPerStep = 3;
    // 튕긴 뒤 면에서 띄워 두는 거리. 다음 판정이 면과 겹친 채로 시작하지 않게 한다. (단위: 월드 거리)
    private const float SurfaceSkin = 0.01f;

    private readonly Collider2D[] _overlapResults = new Collider2D[MaxOverlapCount];
    private Rigidbody2D _rigidbody;
    private CircleCollider2D _collider;
    private PooledObject _pooledObject;
    private ContactFilter2D _playerFilter;
    private int _surfaceLayerMask;

    private Vector2 _direction;
    private float _speed;
    private float _lifetime;
    private float _launchTime;
    private float _damage;
    private GameObject _instigator;

    // 날아가는 중인지. 사라졌으면(풀로 돌아갔으면) false다.
    public bool IsFlying { get; private set; }

    // 발사체 원의 월드 반지름. (콜라이더 반지름 × 스케일)
    private float Radius => _collider.radius * Mathf.Abs(transform.lossyScale.x);

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<CircleCollider2D>();
        TryGetComponent(out _pooledObject);

        _playerFilter = new ContactFilter2D();
        _playerFilter.SetLayerMask(LayerMask.GetMask(PlayerLayerName));
        // 플레이어의 피격 판정은 트리거이므로 트리거도 조회한다.
        _playerFilter.useTriggers = true;
        _surfaceLayerMask = LayerMask.GetMask(GroundLayerName, WallLayerName);
    }

    private void OnDisable()
    {
        IsFlying = false;
        _instigator = null;
    }

    // start에서 direction 방향으로 speed(월드 거리/초)로 날아가기 시작한다. lifetime(초)이 지나면 사라진다.
    // instigator는 피해를 준 쪽으로 전달된다.
    public void Launch(Vector2 start, Vector2 direction, float speed, float lifetime, float damage, GameObject instigator)
    {
        _direction = direction.normalized;
        _speed = speed;
        _lifetime = lifetime;
        _launchTime = Time.time;
        _damage = damage;
        _instigator = instigator;
        IsFlying = true;

        transform.position = start;
        _rigidbody.position = start;
    }

    private void FixedUpdate()
    {
        if (!IsFlying)
        {
            return;
        }

        if (Time.time - _launchTime >= _lifetime)
        {
            Finish();
            return;
        }

        float radius = Radius;
        Vector2 position = Move(_rigidbody.position, radius);
        _rigidbody.MovePosition(position);

        if (TryHitPlayer(position, radius))
        {
            Finish();
        }
    }

    // 이번 물리 프레임에 갈 거리만큼 옮긴 위치를 반환한다. 가는 길에 벽·땅·천장이 있으면 닿은 자리에서 방향을 반사하고, 남은 거리만큼 이어서 간다.
    private Vector2 Move(Vector2 position, float radius)
    {
        float remainingDistance = _speed * Time.fixedDeltaTime;

        for (int i = 0; i < MaxBouncesPerStep && remainingDistance > 0f; i++)
        {
            RaycastHit2D hit = Physics2D.CircleCast(position, radius, _direction, remainingDistance, _surfaceLayerMask);

            if (hit.collider == null)
            {
                return position + _direction * remainingDistance;
            }

            position = hit.centroid + hit.normal * SurfaceSkin;
            remainingDistance -= hit.distance;

            // 면을 향해 가고 있을 때만 튕긴다. (면과 겹친 채 이미 멀어지는 중이면 방향을 그대로 둔다)
            if (Vector2.Dot(_direction, hit.normal) < 0f)
            {
                _direction = Vector2.Reflect(_direction, hit.normal);
            }
        }

        return position;
    }

    // 이번 위치에서 플레이어의 피격 판정과 겹치면 피해를 준다. 닿았으면 true.
    // 플레이어가 무적이거나 막아서 피해가 들어가지 않아도 닿은 것으로 본다. (발사체는 닿으면 사라진다)
    private bool TryHitPlayer(Vector2 position, float radius)
    {
        int overlapCount = Physics2D.OverlapCircle(position, radius, _playerFilter, _overlapResults);

        for (int i = 0; i < overlapCount; i++)
        {
            IDamageable target = _overlapResults[i].GetComponentInParent<IDamageable>();

            if (target == null)
            {
                continue;
            }

            target.TakeDamage(new DamageInfo(_damage, _instigator, _overlapResults[i].ClosestPoint(position)));
            return true;
        }

        return false;
    }

    // 발사체를 없앤다. (풀로 돌아간다) 수명이 끝났거나 플레이어에 닿았을 때, 그리고 Mossback이 죽거나 Release될 때 호출된다.
    public void Finish()
    {
        if (!IsFlying)
        {
            return;
        }

        IsFlying = false;
        _instigator = null;

        if (_pooledObject != null && _pooledObject.IsSpawned && PoolManager.HasInstance)
        {
            PoolManager.Instance.Despawn(gameObject);
            return;
        }

        gameObject.SetActive(false);
    }
}

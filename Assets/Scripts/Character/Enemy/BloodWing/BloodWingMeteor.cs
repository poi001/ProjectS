using UnityEngine;

// BloodWing 스킬(메테오)의 운석 하나. 화면 위에서 수직으로 일정한 속도로 떨어진다. (Enemy_GDD 7.6)
// Fall로 시작한다. 떨어지는 동안에는 EnemySkill 레이어이며, 몸(원)에 닿은 플레이어는 피해를 입는다.
//  - 일반 운석: 플레이어나 바닥에 닿으면 사라진다. (풀로 돌아간다)
//  - 남는 운석: 스킬 한 번에 하나. 플레이어에 닿아도 사라지지 않고(피해는 한 번만) 바닥까지 떨어진 뒤, PlayerSkill 레이어로 바뀌어 바닥에 그대로 남는다.
//    돌진(대시)하던 BloodWing이 부딪히면 사라지고 BloodWing이 기절한다. (Consume)
// 그림은 그레이박스(원)이며, 일반·남는 운석의 색을 다르게 해 구분한다.
// 풀(PoolManager)에서 꺼내 쓰므로 PooledObject의 반환 방식은 Manual로 둔다. 움직임은 Kinematic Rigidbody2D로 물리 프레임에서 옮긴다.
[RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
public class BloodWingMeteor : MonoBehaviour
{
    private const string PlayerLayerName = "Player";
    private const string FallingLayerName = "EnemySkill";
    private const string RemainingLayerName = "PlayerSkill";
    // 한 번의 판정에서 처리할 최대 콜라이더 수
    private const int MaxOverlapCount = 4;

    private enum Phase
    {
        Idle,
        Falling,
        Landed,
    }

    [Tooltip("운석 그림입니다. 일반 운석과 바닥에 남는 운석을 색으로 구분합니다.")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Tooltip("일반 운석의 색입니다. (그레이박스)")]
    [SerializeField] private Color _normalColor = new Color(0.55f, 0.35f, 0.3f, 1f);
    [Tooltip("바닥에 남는 운석의 색입니다. 일반 운석과 구분되게 정합니다. (그레이박스)")]
    [SerializeField] private Color _remainingColor = new Color(0.4f, 0.8f, 1f, 1f);

    private readonly Collider2D[] _overlapResults = new Collider2D[MaxOverlapCount];
    private Rigidbody2D _rigidbody;
    private CircleCollider2D _collider;
    private PooledObject _pooledObject;
    private ContactFilter2D _playerFilter;
    private int _fallingLayer;
    private int _remainingLayer;

    private Phase _phase;
    private float _speed;
    private float _landingY;
    private float _damage;
    private GameObject _instigator;
    private bool _remainsOnGround;
    private bool _hasHitPlayer;

    // 운석 원의 월드 반지름. (콜라이더 반지름 × 스케일)
    public float Radius => _collider.radius * Mathf.Abs(transform.lossyScale.x);
    // 바닥에 남아 있는 운석인지
    public bool IsLanded => _phase == Phase.Landed;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _collider = GetComponent<CircleCollider2D>();
        TryGetComponent(out _pooledObject);

        _playerFilter = new ContactFilter2D();
        _playerFilter.SetLayerMask(LayerMask.GetMask(PlayerLayerName));
        // 플레이어의 피격 판정은 트리거이므로 트리거도 조회한다.
        _playerFilter.useTriggers = true;
        _fallingLayer = LayerMask.NameToLayer(FallingLayerName);
        _remainingLayer = LayerMask.NameToLayer(RemainingLayerName);
    }

    private void OnDisable()
    {
        _phase = Phase.Idle;
        _instigator = null;
    }

    // start에서 수직으로 speed(월드 거리/초)로 떨어지기 시작한다. landingY는 바닥 윗면의 높이다.
    // remainsOnGround가 true면 바닥에 남는 운석이 된다. instigator는 피해를 준 쪽으로 전달된다.
    public void Fall(Vector2 start, float speed, float landingY, float damage, GameObject instigator, bool remainsOnGround)
    {
        _speed = speed;
        _landingY = landingY;
        _damage = damage;
        _instigator = instigator;
        _remainsOnGround = remainsOnGround;
        _hasHitPlayer = false;
        _phase = Phase.Falling;

        gameObject.layer = _fallingLayer;

        if (_spriteRenderer != null)
        {
            _spriteRenderer.color = remainsOnGround ? _remainingColor : _normalColor;
        }

        transform.position = start;
        _rigidbody.position = start;
    }

    private void FixedUpdate()
    {
        if (_phase != Phase.Falling)
        {
            return;
        }

        float radius = Radius;
        Vector2 position = _rigidbody.position;
        position.y -= _speed * Time.fixedDeltaTime;

        float restY = _landingY + radius;
        bool hasLanded = position.y <= restY;

        if (hasLanded)
        {
            position.y = restY;
        }

        _rigidbody.MovePosition(position);

        if (TryHitPlayer(position, radius) && !_remainsOnGround)
        {
            Finish();
            return;
        }

        if (!hasLanded)
        {
            return;
        }

        if (_remainsOnGround)
        {
            Land();
        }
        else
        {
            Finish();
        }
    }

    // 이번 위치에서 플레이어의 피격 판정과 겹치면 피해를 준다. 닿았으면 true. (한 운석이 플레이어를 한 번만 맞힌다)
    // 플레이어가 무적이어도 닿은 것으로 본다. (일반 운석은 닿으면 사라진다)
    private bool TryHitPlayer(Vector2 position, float radius)
    {
        if (_hasHitPlayer)
        {
            return false;
        }

        int overlapCount = Physics2D.OverlapCircle(position, radius, _playerFilter, _overlapResults);

        for (int i = 0; i < overlapCount; i++)
        {
            IDamageable target = _overlapResults[i].GetComponentInParent<IDamageable>();

            if (target == null)
            {
                continue;
            }

            _hasHitPlayer = true;
            target.TakeDamage(new DamageInfo(_damage, _instigator, _overlapResults[i].ClosestPoint(position)));
            return true;
        }

        return false;
    }

    // 바닥에 남은 운석을 쓴다. (돌진하던 BloodWing이 부딪혀 기절할 때) 사라져 풀로 돌아간다.
    public void Consume()
    {
        if (_phase == Phase.Landed)
        {
            Finish();
        }
    }

    // 남는 운석이 바닥에 닿았을 때: 멈추고 PlayerSkill 레이어로 바뀌어 그대로 남는다.
    private void Land()
    {
        _phase = Phase.Landed;
        _instigator = null;
        gameObject.layer = _remainingLayer;
    }

    private void Finish()
    {
        _phase = Phase.Idle;
        _instigator = null;

        if (_pooledObject != null && _pooledObject.IsSpawned && PoolManager.HasInstance)
        {
            PoolManager.Instance.Despawn(gameObject);
            return;
        }

        gameObject.SetActive(false);
    }
}

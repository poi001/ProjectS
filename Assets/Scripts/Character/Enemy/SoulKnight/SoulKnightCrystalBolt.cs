using UnityEngine;

// SoulKnight 2페이즈 기도 기믹에서 결석이 플레이어에게 쏘는 번개 한 발. (Enemy_GDD 6.11)
// Fire로 시작한다. 경고 시간 동안 결석에서 조준 위치까지 가는 선(경고)을 보여 준 뒤, 번개가 조준 위치에 꽂힌다.
// 꽂히는 순간 결석~조준 위치 선 위에 있는 플레이어에게 피해를 준다. 조준 위치는 쏠 때 정해지므로 경고를 보고 움직이면 피할 수 있다.
// 번개 몸통은 거리와 상관없이 같은 굵기로 보이도록 LineRenderer 두 개(빛 번짐 + 심)로 지그재그를 그리고, 짧게 깜빡이며 모양을 바꾼다.
// 맞는 지점 효과(자식 Impact, Flipbook)는 조준 위치에서 한 번 재생된다. 효과가 끝나면 풀로 돌아간다.
// 풀(PoolManager)에서 꺼내 쓰므로 PooledObject의 반환 방식은 Manual로 둔다.
public class SoulKnightCrystalBolt : MonoBehaviour
{
    private const string PlayerLayerName = "Player";
    // 한 번의 판정에서 처리할 최대 콜라이더 수
    private const int MaxCastCount = 4;
    // 지그재그를 이루는 점의 최대 개수
    private const int MaxPointCount = 48;

    private enum Phase
    {
        Idle,
        Warning,
        Striking,
    }

    [Header("그리기")]
    [Tooltip("경고 선입니다. 결석에서 조준 위치까지 곧게 그립니다. 색은 이 LineRenderer의 색을 쓰고, 경고 시간 동안 Warning Start Alpha에서 원래 불투명도로 진해집니다.")]
    [SerializeField] private LineRenderer _warningLine;

    [Tooltip("번개 몸통의 빛 번짐(굵고 옅은 선)입니다. 심과 같은 지그재그를 그립니다.")]
    [SerializeField] private LineRenderer _glowLine;

    [Tooltip("번개 몸통의 심(가늘고 밝은 선)입니다.")]
    [SerializeField] private LineRenderer _coreLine;

    [Tooltip("번개가 꽂힌 지점에서 재생할 효과입니다. Flipbook이 붙어 있어야 하며, 꺼진 채로 두면 꽂힐 때 켭니다.")]
    [SerializeField] private Flipbook _impact;

    [Header("모양")]
    [Tooltip("경고 선이 처음 나타날 때의 불투명도입니다. 경고 시간 동안 원래 불투명도까지 진해집니다. (0 = 투명, 1 = 원래 불투명도 그대로)")]
    [SerializeField, Range(0f, 1f)] private float _warningStartAlpha = 0.25f;

    [Tooltip("지그재그 한 마디의 길이입니다. 짧을수록 잘게 꺾입니다. (단위: 월드 거리)")]
    [SerializeField, Min(0.05f)] private float _segmentLength = 0.35f;

    [Tooltip("지그재그가 곧은 선에서 옆으로 벗어나는 최대 거리입니다. 양 끝으로 갈수록 줄어듭니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _jitter = 0.18f;

    [Tooltip("번개가 꽂힌 채 보이는 시간입니다. 이 시간 동안 깜빡이며 모양을 바꿉니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _boltDuration = 0.12f;

    [Tooltip("번개가 꽂힌 뒤 사라지는 데 걸리는 시간입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _boltFadeDuration = 0.12f;

    [Tooltip("번개 모양을 새로 그리는 간격입니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _flickerInterval = 0.04f;

    [Header("판정")]
    [Tooltip("번개 판정의 반지름입니다. 결석에서 조준 위치까지 이 굵기로 훑어 플레이어의 피격 판정과 겹치면 맞습니다. (단위: 월드 거리)")]
    [SerializeField, Min(0.01f)] private float _hitRadius = 0.2f;

    private readonly RaycastHit2D[] _castResults = new RaycastHit2D[MaxCastCount];
    private readonly Vector3[] _points = new Vector3[MaxPointCount];
    private ContactFilter2D _contactFilter;
    private PooledObject _pooledObject;
    private Color _warningStartColor;
    private Color _warningEndColor;
    private Color _glowStartColor;
    private Color _glowEndColor;
    private Color _coreStartColor;
    private Color _coreEndColor;

    private Phase _phase;
    private Vector2 _from;
    private Vector2 _to;
    private float _warningTime;
    private float _damage;
    private GameObject _instigator;
    private float _elapsedTime;
    private float _nextFlickerTime;
    private int _pointCount;

    // 경고 중이거나 번개·효과가 아직 보이는 중인지
    public bool IsActive => _phase != Phase.Idle;

    private void Awake()
    {
        TryGetComponent(out _pooledObject);

        _contactFilter = new ContactFilter2D();
        _contactFilter.SetLayerMask(LayerMask.GetMask(PlayerLayerName));
        // 플레이어의 피격 판정은 트리거이므로 트리거도 조회한다.
        _contactFilter.useTriggers = true;

        _warningStartColor = _warningLine.startColor;
        _warningEndColor = _warningLine.endColor;
        _glowStartColor = _glowLine.startColor;
        _glowEndColor = _glowLine.endColor;
        _coreStartColor = _coreLine.startColor;
        _coreEndColor = _coreLine.endColor;
    }

    private void OnDisable()
    {
        _phase = Phase.Idle;
        HideAll();
    }

    // from(결석)에서 to(조준 위치)로 번개를 쏜다. warningTime 동안 경고를 보여 준 뒤 꽂힌다.
    // instigator는 피해를 준 쪽으로 전달된다. (플레이어는 이 오브젝트의 반대쪽으로 밀려난다)
    public void Fire(Vector2 from, Vector2 to, float warningTime, float damage, GameObject instigator)
    {
        _from = from;
        _to = to;
        _warningTime = warningTime;
        _damage = damage;
        _instigator = instigator;
        _elapsedTime = 0f;
        _phase = Phase.Warning;

        HideAll();
        _impact.transform.position = to;

        _warningLine.positionCount = 2;
        _warningLine.SetPosition(0, from);
        _warningLine.SetPosition(1, to);
        SetAlpha(_warningLine, _warningStartColor, _warningEndColor, _warningStartAlpha);
        _warningLine.enabled = true;

        if (warningTime <= 0f)
        {
            Strike();
        }
    }

    // 아직 꽂히지 않은 번개는 바로 거둔다. 이미 꽂힌 번개는 남은 연출을 마저 보여 준다. (기믹이 끝날 때 호출)
    public void Cancel()
    {
        if (_phase == Phase.Warning)
        {
            Finish();
        }
    }

    private void Update()
    {
        if (_phase == Phase.Idle)
        {
            return;
        }

        _elapsedTime += Time.deltaTime;

        if (_phase == Phase.Warning)
        {
            float progress = _warningTime > 0f ? Mathf.Clamp01(_elapsedTime / _warningTime) : 1f;
            SetAlpha(_warningLine, _warningStartColor, _warningEndColor, Mathf.Lerp(_warningStartAlpha, 1f, progress));

            if (_elapsedTime >= _warningTime)
            {
                Strike();
            }

            return;
        }

        UpdateBolt(_elapsedTime - _warningTime);
    }

    private void Strike()
    {
        _phase = Phase.Striking;
        _warningLine.enabled = false;
        _nextFlickerTime = 0f;

        BuildZigzag();
        SetAlpha(_glowLine, _glowStartColor, _glowEndColor, 1f);
        SetAlpha(_coreLine, _coreStartColor, _coreEndColor, 1f);
        _glowLine.enabled = true;
        _coreLine.enabled = true;
        _impact.gameObject.SetActive(true);

        HitPlayer();
    }

    // 꽂힌 뒤의 번개: 깜빡이며 모양을 바꾸다가 사라진다. 맞는 지점 효과까지 끝나면 풀로 돌아간다.
    private void UpdateBolt(float timeSinceStrike)
    {
        float visibleTime = _boltDuration + _boltFadeDuration;

        if (timeSinceStrike < visibleTime)
        {
            if (timeSinceStrike >= _nextFlickerTime)
            {
                _nextFlickerTime = timeSinceStrike + _flickerInterval;
                BuildZigzag();
            }

            float alpha = timeSinceStrike <= _boltDuration || _boltFadeDuration <= 0f ? 1f : 1f - (timeSinceStrike - _boltDuration) / _boltFadeDuration;
            SetAlpha(_glowLine, _glowStartColor, _glowEndColor, alpha);
            SetAlpha(_coreLine, _coreStartColor, _coreEndColor, alpha);
        }
        else if (_glowLine.enabled)
        {
            _glowLine.enabled = false;
            _coreLine.enabled = false;
        }

        if (timeSinceStrike >= Mathf.Max(visibleTime, _impact.Duration))
        {
            Finish();
        }
    }

    // 결석에서 조준 위치까지 지그재그 점을 새로 만든다. 양 끝은 고정하고 가운데로 갈수록 크게 벗어난다.
    private void BuildZigzag()
    {
        Vector2 delta = _to - _from;
        float distance = delta.magnitude;
        int segmentCount = Mathf.Clamp(Mathf.CeilToInt(distance / _segmentLength), 1, MaxPointCount - 1);
        Vector2 normal = distance > 0.0001f ? new Vector2(-delta.y, delta.x) / distance : Vector2.zero;

        _pointCount = segmentCount + 1;

        for (int i = 0; i < _pointCount; i++)
        {
            float t = (float)i / segmentCount;
            float taper = Mathf.Sin(t * Mathf.PI);
            Vector2 point = _from + delta * t + normal * (Random.Range(-1f, 1f) * _jitter * taper);
            _points[i] = point;
        }

        _glowLine.positionCount = _pointCount;
        _coreLine.positionCount = _pointCount;

        for (int i = 0; i < _pointCount; i++)
        {
            _glowLine.SetPosition(i, _points[i]);
            _coreLine.SetPosition(i, _points[i]);
        }
    }

    // 결석에서 조준 위치까지 판정 반지름으로 훑어 플레이어의 피격 판정에 피해를 준다. 한 번에 한 대상만 한 번 맞는다.
    private void HitPlayer()
    {
        Vector2 delta = _to - _from;
        float distance = delta.magnitude;
        Vector2 direction = distance > 0.0001f ? delta / distance : Vector2.down;
        int hitCount = Physics2D.CircleCast(_from, _hitRadius, direction, _contactFilter, _castResults, distance);

        for (int i = 0; i < hitCount; i++)
        {
            IDamageable target = _castResults[i].collider.GetComponentInParent<IDamageable>();

            if (target == null)
            {
                continue;
            }

            Vector2 hitPoint = _castResults[i].collider.ClosestPoint(_to);
            target.TakeDamage(new DamageInfo(_damage, _instigator, hitPoint));
            return;
        }
    }

    private void Finish()
    {
        _phase = Phase.Idle;
        _instigator = null;
        HideAll();

        if (_pooledObject != null && _pooledObject.IsSpawned && PoolManager.HasInstance)
        {
            PoolManager.Instance.Despawn(gameObject);
            return;
        }

        gameObject.SetActive(false);
    }

    private void HideAll()
    {
        _warningLine.enabled = false;
        _glowLine.enabled = false;
        _coreLine.enabled = false;
        _impact.gameObject.SetActive(false);
    }

    // 선의 원래 색(인스펙터 값)에 불투명도 배율을 곱해 적용한다.
    private static void SetAlpha(LineRenderer line, Color startColor, Color endColor, float alpha)
    {
        startColor.a *= alpha;
        endColor.a *= alpha;
        line.startColor = startColor;
        line.endColor = endColor;
    }
}

using UnityEngine;

// 풀로 반환되는 방식
public enum PoolReturnMode
{
    // 자동 반환하지 않는다. 사용하는 쪽에서 PoolManager.Despawn을 직접 호출한다.
    Manual,
    // 꺼낸 뒤 지정한 시간(초)이 지나면 반환한다. (스프라이트 애니메이션 FX 등)
    Duration,
    // 파티클 재생이 모두 끝나면 반환한다. (자식 파티클 포함)
    ParticleEnd
}

// 풀링 대상 프리팹(이펙트, 스킬 FX 등)에 붙이는 컴포넌트. 풀 소속 정보와 자동 반환을 담당한다.
// 프리팹에 붙어 있지 않으면 PoolManager가 런타임에 추가하지만, 자동 반환 설정을 위해 프리팹에 직접 붙이는 것을 권장한다.
public class PooledObject : MonoBehaviour
{
    [SerializeField] private PoolReturnMode _returnMode = PoolReturnMode.ParticleEnd;
    // Duration 모드에서 반환까지 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _duration = 1f;

    private ParticleSystem _particleSystem;
    private float _returnTime;

    // 이 인스턴스를 만든 원본 프리팹. 반환할 풀을 찾는 키로 쓴다.
    public GameObject Prefab { get; private set; }
    // 현재 풀에서 꺼내져 사용 중인지 여부
    public bool IsSpawned { get; private set; }
    // 프리팹의 원래 로컬 스케일. 꺼낼 때 복원해서 이전 사용 때 바꾼 스케일(방향 뒤집기 등)이 남지 않게 한다.
    public Vector3 InitialScale { get; private set; }

    private void Awake()
    {
        InitialScale = transform.localScale;

        if (_returnMode == PoolReturnMode.ParticleEnd)
        {
            _particleSystem = GetComponentInChildren<ParticleSystem>(true);

            // 파티클이 없는 프리팹(런타임 자동 추가 포함)은 수동 반환으로 취급한다.
            if (_particleSystem == null)
            {
                _returnMode = PoolReturnMode.Manual;
            }
        }

        // 수동 반환은 매 프레임 검사할 필요가 없다.
        enabled = _returnMode != PoolReturnMode.Manual;
    }

    private void OnEnable()
    {
        if (_returnMode == PoolReturnMode.Duration)
        {
            _returnTime = Time.time + _duration;
        }
        else if (_returnMode == PoolReturnMode.ParticleEnd)
        {
            // 재사용 시 이전 재생 상태가 남지 않도록 처음부터 다시 재생한다.
            _particleSystem.Clear(true);
            _particleSystem.Play(true);
        }
    }

    private void Update()
    {
        // 풀에서 꺼낸 인스턴스만 자동 반환한다. (씬에 직접 배치한 것은 건드리지 않는다)
        if (!IsSpawned)
        {
            return;
        }

        bool finished = _returnMode == PoolReturnMode.Duration
            ? Time.time >= _returnTime
            : !_particleSystem.IsAlive(true);

        if (finished && PoolManager.HasInstance)
        {
            PoolManager.Instance.Despawn(gameObject);
        }
    }

    // PoolManager 전용. 풀이 인스턴스를 만들 때 원본 프리팹을 기록한다.
    public void Initialize(GameObject prefab)
    {
        Prefab = prefab;
    }

    // PoolManager 전용. 꺼내짐/반환 상태를 기록한다.
    public void SetSpawned(bool isSpawned)
    {
        IsSpawned = isSpawned;
    }
}

using System.Collections.Generic;
using UnityEngine;

// 오브젝트 풀 매니저. 타격 이펙트, 스킬 FX처럼 자주 생성/삭제되는 오브젝트를 재사용한다.
// 게임 시작 시 자동으로 생성되고 씬이 바뀌어도 유지된다. 프리팹별로 풀이 따로 만들어지며, 처음 꺼낼 때 자동 생성된다.
// 사용: PoolManager.Instance.Spawn(prefab, position, rotation)
// 반환: PooledObject의 자동 반환 설정을 쓰거나, 수동 반환이면 PoolManager.Instance.Despawn(gameObject)를 호출한다.
// 데미지 숫자는 DamageNumbersPro가 자체 풀링을 하므로 이 풀을 쓰지 않는다.
public class PoolManager : PersistentSingleton<PoolManager>
{
    // 프리팹 → 반환되어 대기 중인 인스턴스 목록
    private readonly Dictionary<GameObject, Stack<PooledObject>> _pools = new Dictionary<GameObject, Stack<PooledObject>>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
        if (HasInstance)
        {
            return;
        }

        GameObject managerObject = new GameObject(nameof(PoolManager));
        managerObject.AddComponent<PoolManager>();
    }

    // 미리 인스턴스를 만들어 둔다. 한 번에 많이 나오는 FX의 첫 사용 시 끊김을 막고 싶을 때만 호출한다.
    // count는 대기 중인 인스턴스가 최소 몇 개가 되도록 할지를 뜻한다.
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null)
        {
            Debug.LogError("[PoolManager] Prewarm할 프리팹이 null입니다.", this);
            return;
        }

        Stack<PooledObject> pool = GetPool(prefab);

        while (pool.Count < count)
        {
            pool.Push(CreateObject(prefab));
        }
    }

    // 풀에서 오브젝트를 꺼내 활성화한다. parent가 null이면 매니저 아래에 둔다.
    // parent를 지정하면 그 오브젝트를 따라다닌다. (반환될 때 매니저 아래로 돌아온다)
    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
    {
        if (prefab == null)
        {
            Debug.LogError("[PoolManager] Spawn할 프리팹이 null입니다.", this);
            return null;
        }

        Stack<PooledObject> pool = GetPool(prefab);
        PooledObject pooled = null;

        // 외부에서 파괴된 인스턴스(부모 파괴, 씬 전환 등)는 건너뛴다.
        while (pooled == null && pool.Count > 0)
        {
            pooled = pool.Pop();
        }

        if (pooled == null)
        {
            pooled = CreateObject(prefab);
        }

        Transform pooledTransform = pooled.transform;
        pooledTransform.SetParent(parent != null ? parent : transform, false);
        pooledTransform.localScale = pooled.InitialScale;
        pooledTransform.SetPositionAndRotation(position, rotation);

        pooled.gameObject.SetActive(true);
        pooled.SetSpawned(true);

        return pooled.gameObject;
    }

    // 오브젝트를 풀로 반환한다. 풀에서 만든 것이 아니면 그냥 파괴한다. 이미 반환된 것을 다시 호출해도 안전하다.
    public void Despawn(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        PooledObject pooled = target.GetComponent<PooledObject>();

        if (pooled == null || pooled.Prefab == null)
        {
            Destroy(target);
            return;
        }

        if (!pooled.IsSpawned)
        {
            return;
        }

        pooled.SetSpawned(false);
        target.SetActive(false);
        target.transform.SetParent(transform, false);
        GetPool(pooled.Prefab).Push(pooled);
    }

    private Stack<PooledObject> GetPool(GameObject prefab)
    {
        if (!_pools.TryGetValue(prefab, out Stack<PooledObject> pool))
        {
            pool = new Stack<PooledObject>();
            _pools.Add(prefab, pool);
        }

        return pool;
    }

    // 비활성 상태의 새 인스턴스를 만든다. (활성화는 Spawn에서 위치를 정한 뒤에 한다)
    private PooledObject CreateObject(GameObject prefab)
    {
        GameObject instance = Instantiate(prefab, transform);
        instance.name = prefab.name;

        PooledObject pooled = instance.GetComponent<PooledObject>();

        if (pooled == null)
        {
            Debug.LogWarning($"[PoolManager] {prefab.name} 프리팹에 PooledObject가 없어 자동으로 추가합니다. (수동 반환으로 동작)", prefab);
            pooled = instance.AddComponent<PooledObject>();
        }

        pooled.Initialize(prefab);
        instance.SetActive(false);

        return pooled;
    }
}

using UnityEngine;

// 피격 시 피격 위치에 타격 VFX를 띄운다. 전투 씬의 BattleManagers에 포함되어 있으며, VFX 프리팹을 연결한다.
// VFX는 PoolManager로 꺼내며, 반환은 프리팹 쪽(Flipbook 재생 종료, PooledObject 자동 반환 등)이 담당한다.
// 플레이어가 맞을 때와 그 외(적)가 맞을 때 서로 다른 프리팹을 쓰고, 비워 둔 쪽은 띄우지 않는다.
// 좌우 방향: 프리팹 원본은 오른쪽으로 타격하는 모습 기준이다.
// 공격자가 피격자의 오른쪽에 있으면(왼쪽으로 타격) 스케일 x를 뒤집는다. (스케일은 PoolManager가 꺼낼 때 원래대로 복원한다)
// 전투 시작 시 연결된 프리팹을 풀에 미리 등록(Prewarm)해 첫 타격 때 생성 비용으로 끊기지 않게 한다.
// 미리 만든 개수를 넘으면 풀이 필요한 만큼 추가로 만든다.
public class HitVfxManager : MonoBehaviour
{
    [SerializeField] private GameObject _enemyHitVfxPrefab;
    [SerializeField] private GameObject _playerHitVfxPrefab;
    // 프리팹별로 미리 만들어 둘 개수. 동시에 화면에 보이는 최대 개수 정도로 잡는다.
    [SerializeField, Min(0)] private int _prewarmCount = 5;

    private void OnEnable()
    {
        EventBus<DamageTakenEvent>.Subscribe(OnDamageTaken);
    }

    private void OnDisable()
    {
        EventBus<DamageTakenEvent>.Unsubscribe(OnDamageTaken);
    }

    private void Start()
    {
        if (!PoolManager.HasInstance)
        {
            Debug.LogError("[HitVfxManager] 씬에 PoolManager가 없어 타격 VFX를 띄울 수 없습니다.", this);
            return;
        }

        PrewarmPool(_enemyHitVfxPrefab);
        PrewarmPool(_playerHitVfxPrefab);
    }

    // 비워 둔 슬롯은 건너뛴다.
    private void PrewarmPool(GameObject prefab)
    {
        if (prefab == null)
        {
            return;
        }

        PoolManager.Instance.Prewarm(prefab, _prewarmCount);
    }

    private void OnDamageTaken(DamageTakenEvent damageEvent)
    {
        GameObject prefab = damageEvent.Target.TryGetComponent(out Player _) ? _playerHitVfxPrefab : _enemyHitVfxPrefab;

        if (prefab == null || !PoolManager.HasInstance)
        {
            return;
        }

        GameObject vfx = PoolManager.Instance.Spawn(prefab, damageEvent.HitPoint, Quaternion.identity);

        if (vfx != null && IsHitFromRight(damageEvent))
        {
            Transform vfxTransform = vfx.transform;
            Vector3 scale = vfxTransform.localScale;
            scale.x = -scale.x;
            vfxTransform.localScale = scale;
        }
    }

    // 공격자가 피격자보다 오른쪽에 있는지 여부. 공격자 정보가 없으면 원본 방향(오른쪽 타격)으로 둔다.
    private static bool IsHitFromRight(DamageTakenEvent damageEvent)
    {
        if (damageEvent.Instigator == null)
        {
            return false;
        }

        return damageEvent.Instigator.transform.position.x > damageEvent.Target.transform.position.x;
    }
}

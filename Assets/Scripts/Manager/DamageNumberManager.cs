using DamageNumbersPro;
using UnityEngine;

// 피격 시 데미지 숫자를 띄운다. 씬에 하나 배치하고 DamageNumbersPro 프리팹을 연결한다.
// 풀링은 DamageNumbersPro 자체 기능을 쓰므로, 프리팹의 Enable Pooling을 켜 둔다. (PoolManager는 사용하지 않는다)
// 플레이어가 맞을 때와 그 외(적)가 맞을 때 서로 다른 프리팹을 쓴다.
public class DamageNumberManager : MonoBehaviour
{
    [SerializeField] private DamageNumber _enemyHitPrefab;
    [SerializeField] private DamageNumber _playerHitPrefab;

    private void OnEnable()
    {
        EventBus<DamageTakenEvent>.Subscribe(OnDamageTaken);
    }

    private void OnDisable()
    {
        EventBus<DamageTakenEvent>.Unsubscribe(OnDamageTaken);
    }

    private void OnDamageTaken(DamageTakenEvent damageEvent)
    {
        DamageNumber prefab = damageEvent.Target.TryGetComponent(out Player _) ? _playerHitPrefab : _enemyHitPrefab;

        if (prefab == null)
        {
            return;
        }

        prefab.Spawn(damageEvent.HitPoint, damageEvent.Amount);
    }
}

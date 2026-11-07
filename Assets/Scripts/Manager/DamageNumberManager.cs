using DamageNumbersPro;
using UnityEngine;

// 피격 시 데미지 숫자를 띄운다. 전투 씬의 BattleManagers에 포함되어 있으며, DamageNumbersPro 프리팹을 연결한다.
// 풀링은 DamageNumbersPro 자체 기능을 쓰므로, 프리팹의 Enable Pooling을 켜 둔다. (PoolManager는 사용하지 않는다)
// 플레이어가 맞을 때와 그 외(적)가 맞을 때 서로 다른 프리팹을 쓴다.
public class DamageNumberManager : MonoBehaviour
{
    [SerializeField] private DamageNumber _enemyHitPrefab;
    [SerializeField] private DamageNumber _playerHitPrefab;
    [SerializeField] private DamageNumber _healPrefab;

    private void OnEnable()
    {
        EventBus<DamageTakenEvent>.Subscribe(OnDamageTaken);
        EventBus<HealEvent>.Subscribe(OnHealed);
    }

    private void OnDisable()
    {
        EventBus<DamageTakenEvent>.Unsubscribe(OnDamageTaken);
        EventBus<HealEvent>.Unsubscribe(OnHealed);
    }

    private void OnDamageTaken(DamageTakenEvent damageEvent)
    {
        bool isPlayer = damageEvent.Target.TryGetComponent(out Player player);
        DamageNumber prefab = isPlayer ? _playerHitPrefab : _enemyHitPrefab;

        if (prefab == null)
        {
            return;
        }

        // 플레이어는 맞은 지점과 관계없이 머리 위에, 그 외(적)는 맞은 지점에 띄운다.
        Vector2 spawnPosition = isPlayer ? GetHeadPosition(player) : damageEvent.HitPoint;
        prefab.Spawn(spawnPosition, damageEvent.Amount);
    }

    // 피격 콜라이더의 맨 위 가운데. 피격 콜라이더가 없으면 캐릭터 위치를 쓴다.
    private static Vector2 GetHeadPosition(Player player)
    {
        if (player.Hurtbox == null)
        {
            return player.transform.position;
        }

        Bounds bounds = player.Hurtbox.bounds;
        return new Vector2(bounds.center.x, bounds.max.y);
    }

    private void OnHealed(HealEvent healEvent)
    {
        if (_healPrefab == null)
        {
            return;
        }

        _healPrefab.Spawn(healEvent.Position, healEvent.Amount);
    }
}

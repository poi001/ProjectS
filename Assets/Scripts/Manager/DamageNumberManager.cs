using DamageNumbersPro;
using UnityEngine;

// 피격 시 데미지 숫자를 띄운다. 전투 씬의 BattleManagers에 포함되어 있으며, DamageNumbersPro 프리팹을 연결한다.
// 풀링은 DamageNumbersPro 자체 기능을 쓰므로, 프리팹의 Enable Pooling을 켜 둔다. (PoolManager는 사용하지 않는다)
// 플레이어가 맞을 때와 그 외(적)가 맞을 때 서로 다른 프리팹을 쓴다. 적이 치명타를 맞으면 치명타용 프리팹을 쓴다.
public class DamageNumberManager : MonoBehaviour
{
    [Tooltip("적이 피해를 입을 때(플레이어의 일반 타격) 띄우는 숫자 프리팹입니다. (주황)")]
    [SerializeField] private DamageNumber _enemyHitPrefab;
    [Tooltip("적이 치명타 피해를 입을 때 띄우는 숫자 프리팹입니다. (빨강, 일반 타격보다 크게)\n비워 두면 치명타에도 일반 타격 숫자를 띄웁니다.")]
    [SerializeField] private DamageNumber _enemyCritPrefab;
    [Tooltip("플레이어가 피해를 입을 때 머리 위에 띄우는 숫자 프리팹입니다. (어두운 보라)")]
    [SerializeField] private DamageNumber _playerHitPrefab;
    [Tooltip("회복할 때 띄우는 숫자 프리팹입니다. 대상이 플레이어든 적이든 같습니다. (초록)")]
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
        DamageNumber prefab = isPlayer ? _playerHitPrefab : GetEnemyHitPrefab(damageEvent.IsCritical);

        if (prefab == null)
        {
            return;
        }

        // 플레이어는 맞은 지점과 관계없이 머리 위에, 그 외(적)는 맞은 지점에 띄운다.
        Vector2 spawnPosition = isPlayer ? GetHeadPosition(player) : damageEvent.HitPoint;
        prefab.Spawn(spawnPosition, damageEvent.Amount);
    }

    // 적이 맞을 때의 숫자 프리팹. 치명타면 치명타용을, 치명타용이 없으면 일반 타격용을 쓴다.
    private DamageNumber GetEnemyHitPrefab(bool isCritical)
    {
        if (isCritical && _enemyCritPrefab != null)
        {
            return _enemyCritPrefab;
        }

        return _enemyHitPrefab;
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

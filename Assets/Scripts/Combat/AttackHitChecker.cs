using System.Collections.Generic;
using UnityEngine;

// 플레이어/적 공용 공격 판정기. 컴포넌트가 아니라 Character가 하나씩 만들어 보관한다. (Character.HitChecker)
// 호출된 시점에 판정 범위(HitboxShape)를 OverlapBox로 조회해 겹친 피격 콜라이더에 피해를 준다.
// 언제 때릴지(타격 시점), 어디를 때릴지(범위), 얼마나 때릴지(데미지)는 호출하는 쪽(공격 상태)이 정한다.
// 같은 팀을 때리지 않는 것은 대상 레이어로 보장한다. (플레이어의 공격: Enemy, 적의 공격: Player)
public class AttackHitChecker
{
    // 한 번의 조회에서 처리할 최대 콜라이더 수
    private const int MaxOverlapCount = 8;

    private readonly Collider2D[] _overlapResults = new Collider2D[MaxOverlapCount];
    // 이번 공격에서 이미 맞힌 대상. 지속 판정(칼 휘두름 구간 등)에서 같은 대상을 여러 번 때리지 않기 위함이다.
    // 한 캐릭터는 한 번에 하나의 공격만 하므로 기록은 하나만 둔다.
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private readonly Transform _ownerTransform;
    private readonly GameObject _owner;
    private ContactFilter2D _contactFilter;

    // 마지막으로 판정이 나간 범위와 시각. 디버그로 범위를 보여 줄 때만 쓴다.
    public Vector2 LastHitCenter { get; private set; }
    public Vector2 LastHitSize { get; private set; }
    public float LastHitTime { get; private set; } = float.NegativeInfinity;

    public AttackHitChecker(Character owner, LayerMask targetLayers)
    {
        _owner = owner.gameObject;
        _ownerTransform = owner.transform;

        // 피격 콜라이더는 트리거이므로 트리거도 조회 대상에 포함한다.
        _contactFilter = new ContactFilter2D();
        _contactFilter.SetLayerMask(targetLayers);
        _contactFilter.useTriggers = true;
    }

    // 맞힌 대상 기록을 비운다. 새 공격(타격 이벤트, 휘두름 판정 구간 시작, 슬라이딩 충격 직전 등)을 시작할 때 호출한다.
    public void ResetHitTargets()
    {
        _hitTargets.Clear();
    }

    // 이번 공격에서 아직 맞지 않은 대상에게만 피해를 준다. 피해를 준 대상 수를 반환한다.
    // facingDirection: 캐릭터가 보는 방향 (오른쪽 1, 왼쪽 -1)
    public int Hit(HitboxShape hitbox, float damage, float facingDirection)
    {
        Vector2 center = hitbox.GetCenter(_ownerTransform.position, facingDirection);
        LastHitCenter = center;
        LastHitSize = hitbox.Size;
        LastHitTime = Time.time;

        int overlapCount = Physics2D.OverlapBox(center, hitbox.Size, 0f, _contactFilter, _overlapResults);
        int hitCount = 0;

        for (int i = 0; i < overlapCount; i++)
        {
            Collider2D hurtbox = _overlapResults[i];
            IDamageable target = hurtbox.GetComponentInParent<IDamageable>();

            if (target == null || !_hitTargets.Add(target))
            {
                continue;
            }

            DamageInfo damageInfo = new DamageInfo(damage, _owner, hurtbox.ClosestPoint(center));

            if (target.TakeDamage(damageInfo))
            {
                hitCount++;
            }
        }

        return hitCount;
    }
}

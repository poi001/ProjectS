using System.Collections.Generic;
using UnityEngine;

// 플레이어/적 공용 공격 판정. 호출된 시점에 박스 영역을 Overlap으로 조회해 겹친 피격 콜라이더에 피해를 준다.
// 언제 때릴지(타격 시점)와 얼마나 때릴지(데미지)는 호출하는 쪽(공격 상태)이 정한다.
// 공격마다 범위가 다르면 이 컴포넌트를 공격 수만큼 두고 값(오프셋, 크기)만 다르게 설정한다.
// 같은 팀을 때리지 않는 것은 대상 레이어 마스크로 보장한다. (플레이어: Enemy, 적: Player)
public class AttackHitbox : MonoBehaviour
{
    // 한 번의 조회에서 처리할 최대 콜라이더 수
    private const int MaxOverlapCount = 8;

    [SerializeField] private LayerMask _targetLayers;
    // 캐릭터(루트) 위치 기준 오프셋. 오른쪽을 볼 때 기준이며, 왼쪽을 보면 x를 뒤집는다.
    [SerializeField] private Vector2 _offset = new Vector2(1f, 0f);
    [SerializeField] private Vector2 _size = Vector2.one;

    private readonly Collider2D[] _overlapResults = new Collider2D[MaxOverlapCount];
    // 이번 공격에서 이미 맞힌 대상. 지속 판정(슬라이딩 등)에서 같은 대상을 여러 번 때리지 않기 위함이다.
    private readonly HashSet<IDamageable> _hitTargets = new HashSet<IDamageable>();
    private ContactFilter2D _contactFilter;
    private Transform _ownerTransform;
    private GameObject _owner;
    private float _lastFacingDirection = 1f;

    private void Awake()
    {
        Character owner = GetComponentInParent<Character>();

        if (owner == null)
        {
            Debug.LogError($"{name}: 부모 계층에 Character가 없습니다.", this);
            enabled = false;
            return;
        }

        _owner = owner.gameObject;
        _ownerTransform = owner.transform;

        // 피격 콜라이더는 트리거이므로 트리거도 조회 대상에 포함한다.
        _contactFilter = new ContactFilter2D();
        _contactFilter.SetLayerMask(_targetLayers);
        _contactFilter.useTriggers = true;
    }

    // 맞힌 대상 기록을 비운다. 새 공격(타격 이벤트, 슬라이딩 시작 등)을 시작할 때 호출한다.
    public void ResetHitTargets()
    {
        _hitTargets.Clear();
    }

    // 이번 공격에서 아직 맞지 않은 대상에게만 피해를 준다. 피해를 준 대상 수를 반환한다.
    // facingDirection: 캐릭터가 보는 방향 (오른쪽 1, 왼쪽 -1)
    public int Hit(float damage, float facingDirection)
    {
        if (!enabled)
        {
            return 0;
        }

        _lastFacingDirection = facingDirection;
        Vector2 center = GetCenter(_ownerTransform.position, facingDirection);
        int overlapCount = Physics2D.OverlapBox(center, _size, 0f, _contactFilter, _overlapResults);
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

    private Vector2 GetCenter(Vector2 ownerPosition, float facingDirection)
    {
        return ownerPosition + new Vector2(_offset.x * facingDirection, _offset.y);
    }

    // 선택했을 때 판정 범위를 그린다. 플레이 중에는 마지막으로 공격한 방향 기준이다.
    private void OnDrawGizmosSelected()
    {
        Character owner = GetComponentInParent<Character>();

        if (owner == null)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(GetCenter(owner.transform.position, _lastFacingDirection), _size);
    }
}

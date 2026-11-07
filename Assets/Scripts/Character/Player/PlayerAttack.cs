using System;
using UnityEngine;

// 플레이어 공격 하나(지상 콤보 한 단계)의 설정. 피해 배율과 판정 범위를 정한다.
// 언제 때리는지는 공격 클립의 애니메이션 이벤트(타격 프레임)가 정한다.
[Serializable]
public class PlayerAttack
{
    [Tooltip("이 공격의 피해량 배율입니다. 실제 피해 = 플레이어 공격력 × 이 값입니다.\n1 = 공격력 그대로")]
    [SerializeField, Min(0f)] private float _damageMultiplier = 1f;
    [Tooltip("이 공격의 판정 범위입니다. Player를 선택하면 Scene 뷰에 범위가 붉은 선으로 보입니다.")]
    [SerializeField] private HitboxShape _hitbox = new HitboxShape(new Vector2(1f, 0f), new Vector2(1.5f, 1f));

    public float DamageMultiplier => _damageMultiplier;
    public HitboxShape Hitbox => _hitbox;

    public PlayerAttack(float damageMultiplier, HitboxShape hitbox)
    {
        _damageMultiplier = damageMultiplier;
        _hitbox = hitbox;
    }
}

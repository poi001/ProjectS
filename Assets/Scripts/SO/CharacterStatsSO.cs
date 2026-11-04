using UnityEngine;

// 플레이어/적 공통 스탯의 기반 클래스. 에셋 자체가 수정되므로 런타임에 값을 바꾸지 않는다.
// 캐릭터 종류별 스탯은 자식 클래스(PlayerStatsSO, EnemyStatsSO)에 둔다.
public abstract class CharacterStatsSO : ScriptableObject
{
    [SerializeField, Min(0f)] private float _attackPower = 10f;
    // 1이 기본 속도. 플레이어는 공격 애니메이션, 적은 패턴의 재생 속도 배율로 쓰인다.
    [SerializeField, Min(0.1f)] private float _attackSpeed = 1f;
    [SerializeField, Min(1f)] private float _maxHealth = 100f;

    public float AttackPower => _attackPower;
    public float AttackSpeed => _attackSpeed;
    public float MaxHealth => _maxHealth;
}

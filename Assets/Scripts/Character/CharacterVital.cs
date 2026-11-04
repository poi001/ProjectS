using System;
using UnityEngine;

// 플레이어/적 공용. 캐릭터의 현재 체력, 피격, 사망을 담당한다. 데미지를 "받는" 쪽만 처리하고, 주는 쪽은 공격 로직이 담당한다.
// 최대 체력은 각 캐릭터(Player, Enemy)가 초기화할 때 스탯에서 받아온다.
public class CharacterVital : MonoBehaviour, IDamageable
{
    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;

    public event Action<DamageInfo> Damaged;
    public event Action Died;

    public void Initialize(float maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
    }

    public bool TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead || damageInfo.Amount <= 0f)
        {
            return false;
        }

        float previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - damageInfo.Amount);
        Damaged?.Invoke(damageInfo);

        // 연출 시스템(데미지 숫자 등)용. 체력보다 큰 피해는 실제로 깎인 양만 알린다.
        EventBus<DamageTakenEvent>.Publish(new DamageTakenEvent(gameObject, previousHealth - CurrentHealth, damageInfo.HitPoint));

        if (IsDead)
        {
            Died?.Invoke();
        }

        return true;
    }
}

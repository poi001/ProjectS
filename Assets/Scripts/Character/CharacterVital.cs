using System;
using UnityEngine;

// 플레이어/적 공용. 캐릭터의 현재 체력, 피격, 사망을 담당한다. 데미지를 "받는" 쪽만 처리하고, 주는 쪽은 공격 로직이 담당한다.
// 최대 체력은 각 캐릭터(Player, Enemy)가 초기화할 때 스탯에서 받아온다.
public class CharacterVital : MonoBehaviour, IDamageable
{
    // 무적이 끝나는 시각. Time.time 기준이라 타임스케일이 0이면(히트스톱 등) 무적 시간도 함께 멈춘다.
    private float _invincibleEndTime = float.NegativeInfinity;

    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public bool IsDead => CurrentHealth <= 0f;
    // 무적 중에는 피해를 입지 않는다. (피해 이벤트, 데미지 숫자, 피격음도 발생하지 않는다.)
    public bool IsInvincible => Time.time < _invincibleEndTime;

    public event Action<DamageInfo> Damaged;
    public event Action Died;
    // 초기화·피해·회복으로 체력이 바뀔 때마다 호출된다. (체력바 등 이 캐릭터를 직접 참조하는 UI용) 바뀐 값은 CurrentHealth/MaxHealth로 읽는다.
    public event Action HealthChanged;

    public void Initialize(float maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        _invincibleEndTime = float.NegativeInfinity;
        HealthChanged?.Invoke();
    }

    // 지금부터 duration초 동안 무적으로 만든다. 이미 더 길게 남은 무적이 있다면 그대로 둔다.
    public void SetInvincible(float duration)
    {
        _invincibleEndTime = Mathf.Max(_invincibleEndTime, Time.time + duration);
    }

    public bool TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead || IsInvincible || damageInfo.Amount <= 0f)
        {
            return false;
        }

        float previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - damageInfo.Amount);
        HealthChanged?.Invoke();
        Damaged?.Invoke(damageInfo);

        // 연출 시스템(데미지 숫자 등)용. 체력보다 큰 피해는 실제로 깎인 양만 알린다.
        EventBus<DamageTakenEvent>.Publish(new DamageTakenEvent(gameObject, damageInfo.Instigator, previousHealth - CurrentHealth, damageInfo.HitPoint));

        if (IsDead)
        {
            Died?.Invoke();
            // 연출 시스템(KO 연출 등)용. 사망 상태 전환까지 끝난 뒤에 알린다.
            EventBus<CharacterDiedEvent>.Publish(new CharacterDiedEvent(gameObject));
        }

        return true;
    }

    public bool Heal(float healAmount)
    {
        if (IsDead || healAmount <= 0f || CurrentHealth >= MaxHealth)
        {
            return false;
        }

        float previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Min(MaxHealth, CurrentHealth + healAmount);
        HealthChanged?.Invoke();

        // 연출 시스템(회복 숫자 등)용. 최대 체력을 넘는 회복량은 실제로 오른 양만 알린다.
        EventBus<HealEvent>.Publish(new HealEvent(gameObject, CurrentHealth - previousHealth, transform.position));

        return true;
    }
}

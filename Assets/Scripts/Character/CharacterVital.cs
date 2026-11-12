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
    // 피해 무효 중에는 맞기는 하지만 체력이 줄지 않는다. (SoulKnight의 기도 등)
    // 무적과 달리 맞은 것으로 처리해 히트 이펙트와 0 데미지 숫자가 뜨고, Damaged 대신 DamageNullified가 발생한다.
    public bool IsDamageNullified { get; private set; }
    // 피해로 체력이 이 값 아래로 내려가지 않는다. 기본 0이며, 0보다 크면 피해로는 죽지 않는다. (SoulKnight의 각성 전 보호 등)
    // 회복이나 체력 직접 설정에는 영향을 주지 않는다.
    public float MinimumHealth { get; private set; }

    public event Action<DamageInfo> Damaged;
    // 피해 무효 중에 맞았을 때 호출된다. 체력은 그대로이며 Damaged는 호출되지 않는다. (피해 무효 전용 피격음 등)
    public event Action<DamageInfo> DamageNullified;
    public event Action Died;
    // 초기화·피해·회복으로 체력이 바뀔 때마다 호출된다. (체력바 등 이 캐릭터를 직접 참조하는 UI용) 바뀐 값은 CurrentHealth/MaxHealth로 읽는다.
    public event Action HealthChanged;

    public void Initialize(float maxHealth)
    {
        MaxHealth = maxHealth;
        CurrentHealth = maxHealth;
        _invincibleEndTime = float.NegativeInfinity;
        IsDamageNullified = false;
        MinimumHealth = 0f;
        HealthChanged?.Invoke();
    }

    // 피해로 내려갈 수 있는 최소 체력을 정한다. 0이면 제한 없이 피해로 죽을 수 있다.
    // 최소 체력에 걸려 깎이지 않은 피해도 맞은 것으로 처리된다. (피해량은 실제로 깎인 양인 0으로 알린다)
    public void SetMinimumHealth(float minimumHealth)
    {
        MinimumHealth = Mathf.Max(0f, minimumHealth);
    }

    // 피해 무효를 켜거나 끈다. 켠 쪽(상태 등)이 끝날 때 반드시 끈다.
    public void SetDamageNullified(bool isNullified)
    {
        IsDamageNullified = isNullified;
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

        // 맞은 것으로 처리하되 피해는 0으로 알린다. (히트 이펙트와 0 데미지 숫자는 DamageTakenEvent 구독자가 띄운다)
        if (IsDamageNullified)
        {
            DamageNullified?.Invoke(damageInfo);
            EventBus<DamageTakenEvent>.Publish(new DamageTakenEvent(gameObject, damageInfo.Instigator, 0f, damageInfo.HitPoint));
            return true;
        }

        float previousHealth = CurrentHealth;
        CurrentHealth = Mathf.Max(Mathf.Min(previousHealth, MinimumHealth), previousHealth - damageInfo.Amount);
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

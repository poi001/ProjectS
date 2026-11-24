using System;
using UnityEngine;

// 전투 중 플레이어의 현재 기력. (GDD 4장 최대기력) 최대량은 최종 스탯(PlayerStats.MaxStamina)을 따른다.
// Player가 하나 가지고 Init할 때마다 가득 채운다. (Player.Stamina) 같은 인스턴스를 계속 쓰므로 기력바는 한 번만 연결하면 된다.
// 이 클래스는 값만 보관한다. 언제 얼마나 소모하고 회복할지(동작별 소모량, 회복 지연, 초당기력회복력)는 Player가 정해 Drain·Restore를 부른다. (GDD 4.5) Consume은 지금 쓰는 곳이 없다.
// 값이 바뀌면 Changed로 알린다. (기력바 StaminaBar가 받아 즉시 갱신한다)
public class PlayerStamina
{
    public float CurrentStamina { get; private set; }
    public float MaxStamina { get; private set; }
    // 현재 기력 / 최대 기력 (0 ~ 1). 최대 기력이 0이면 0이다.
    public float Ratio => MaxStamina > 0f ? Mathf.Clamp01(CurrentStamina / MaxStamina) : 0f;

    public event Action Changed;

    // 최대 기력을 정하고 가득 채운다. (전투 시작 시)
    public void Initialize(float maxStamina)
    {
        MaxStamina = Mathf.Max(0f, maxStamina);
        CurrentStamina = MaxStamina;
        Changed?.Invoke();
    }

    // 최대 기력을 바꾼다. (룬·장비 보정 등) 체력처럼 현재 기력의 비율을 유지한다.
    public void SetMaxStamina(float maxStamina)
    {
        maxStamina = Mathf.Max(0f, maxStamina);

        if (Mathf.Approximately(maxStamina, MaxStamina))
        {
            return;
        }

        float ratio = MaxStamina > 0f ? CurrentStamina / MaxStamina : 1f;
        MaxStamina = maxStamina;
        CurrentStamina = Mathf.Clamp(MaxStamina * ratio, 0f, MaxStamina);
        Changed?.Invoke();
    }

    // 기력이 충분한지 확인한다.
    public bool HasStamina(float amount)
    {
        return CurrentStamina >= amount;
    }

    // 기력을 소모한다. 모자라면 소모하지 않고 false를 돌려준다.
    public bool Consume(float amount)
    {
        if (amount <= 0f)
        {
            return true;
        }

        if (!HasStamina(amount))
        {
            return false;
        }

        CurrentStamina -= amount;
        Changed?.Invoke();
        return true;
    }

    // 기력을 있는 만큼만 소모한다. 모자라면 0까지만 닳는다. (벽타기처럼 시간에 따라 조금씩 닳는 소모용)
    public void Drain(float amount)
    {
        if (amount <= 0f || CurrentStamina <= 0f)
        {
            return;
        }

        CurrentStamina = Mathf.Max(0f, CurrentStamina - amount);
        Changed?.Invoke();
    }

    // 기력을 회복한다. 최대 기력을 넘지 않는다.
    public void Restore(float amount)
    {
        if (amount <= 0f || CurrentStamina >= MaxStamina)
        {
            return;
        }

        CurrentStamina = Mathf.Min(MaxStamina, CurrentStamina + amount);
        Changed?.Invoke();
    }
}

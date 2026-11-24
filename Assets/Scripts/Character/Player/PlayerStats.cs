using System;
using System.Collections.Generic;

// 전투 중 플레이어의 최종 스탯. (GDD 4장) 기본값(PlayerStatsSO)에 % 보정을 적용한 값을 계산해 보관한다.
// 스탯 에셋은 런타임에 바꾸지 않으므로, 룬·장비 등으로 달라지는 값은 모두 이 클래스가 맡는다. Player가 Init할 때 하나 만들어 가진다. (Player.Stats)
//
// 계산 규칙
// - 보정은 모두 %이며, 같은 스탯에 걸린 보정은 먼저 모두 더한 뒤 한 번에 적용한다. (합연산. +10%와 +20%는 +30%)
// - 최대체력·공격력·공격속도·이동속도·최대기력·초당기력회복력: 기본값 × (1 + 보정 합 / 100)
// - 재사용 대기시간 감소·치명타 확률·피해 흡수량: 값 자체가 %이므로 기본값 + 보정 합 (기본 5%에 +10%면 15%)
// - 최종 값은 소수점 셋째 자리에서 반올림해 둘째 자리까지 둔다. (1000.0015 → 1000.00, 반은 올림)
//   float 오차로 반올림이 어긋나지 않도록 계산과 반올림은 decimal로 한다.
// - 정수로 보여 주는 UI는 ToDisplayInt로 소수점 첫째 자리에서 반올림해 표시한다.
//
// 보정을 거는 쪽(패시브 룬, 장비 등)은 AddModifier에 자기 자신을 source로 넘기고, 효과가 끝나면 RemoveModifiers(source)로 한꺼번에 걷는다.
// 값이 바뀐 스탯은 Changed로 알린다. (최대 체력, 이동속도처럼 다른 컴포넌트에 넣어 둔 값은 Player가 받아 다시 넣는다)
public class PlayerStats : IDisposable
{
    private struct ModifierEntry
    {
        public PlayerStatType StatType;
        public float Percent;
        public object Source;
    }

    // 최종 값에 남기는 소수 자릿수 (소수점 셋째 자리에서 반올림)
    private const int DecimalPlaces = 2;
    // 값 자체가 %인 스탯 중 확률·감소율의 상한
    private const decimal PercentMax = 100m;
    // 계산에 넣는 기본값·보정 값의 절댓값 한도. decimal 계산이 넘치지 않게 하는 안전 범위로, 실제 스탯이 닿을 일은 없다.
    private const float MaxInputValue = 1000000000f;

    private static readonly int StatCount = Enum.GetValues(typeof(PlayerStatType)).Length;

    private readonly PlayerStatsSO _baseStats;
    private readonly float[] _values = new float[StatCount];
    // 다시 계산할 때 값이 바뀐 스탯을 표시해 두는 임시 버퍼. 모든 값을 갱신한 뒤에 한꺼번에 알리기 위함이다.
    private readonly bool[] _changedFlags = new bool[StatCount];
    private readonly List<ModifierEntry> _modifiers = new List<ModifierEntry>();
    private readonly Action _baseStatsChangedHandler;
    // 다시 계산해 알리는 중인지 여부와, 그 도중에 다시 계산해 달라는 요청이 들어왔는지 여부 (Recalculate)
    private bool _isRecalculating;
    private bool _needsRecalculate;

    // 최종 값이 바뀐 스탯마다 호출된다. 호출 시점에는 모든 스탯이 이미 새 값이다.
    public event Action<PlayerStatType> Changed;

    public float MaxHealth => Get(PlayerStatType.MaxHealth);
    public float AttackPower => Get(PlayerStatType.AttackPower);
    public float AttackSpeed => Get(PlayerStatType.AttackSpeed);
    public float MoveSpeed => Get(PlayerStatType.MoveSpeed);
    // 단위: % (0 ~ 100)
    public float CooldownReduction => Get(PlayerStatType.CooldownReduction);
    public float MaxStamina => Get(PlayerStatType.MaxStamina);
    // 단위: % (0 ~ 100)
    public float CritChance => Get(PlayerStatType.CritChance);
    // 단위: %
    public float LifeSteal => Get(PlayerStatType.LifeSteal);
    // 단위: 기력/초
    public float StaminaRegen => Get(PlayerStatType.StaminaRegen);

    public PlayerStats(PlayerStatsSO baseStats)
    {
        _baseStats = baseStats;
        _baseStatsChangedHandler = Recalculate;
        // 플레이 중에 에디터에서 스탯 에셋 값을 고치면 바로 반영한다. (빌드에서는 호출되지 않는다)
        _baseStats.Changed += _baseStatsChangedHandler;

        for (int i = 0; i < StatCount; i++)
        {
            _values[i] = Calculate((PlayerStatType)i);
        }
    }

    // 스탯 에셋의 구독을 푼다. 이 스탯을 가진 쪽(Player)이 Release할 때 호출한다.
    public void Dispose()
    {
        _baseStats.Changed -= _baseStatsChangedHandler;
    }

    // 보정까지 적용한 최종 값
    public float Get(PlayerStatType statType)
    {
        return _values[(int)statType];
    }

    // 보정을 적용하기 전의 기본값
    public float GetBase(PlayerStatType statType)
    {
        return _baseStats.GetBaseValue(statType);
    }

    // 스탯에 % 보정을 건다. percent: 양수는 증가, 음수는 감소 (단위: %)
    // source: 보정을 건 쪽(룬, 장비 등). 나중에 RemoveModifiers로 걷을 때의 열쇠다.
    public void AddModifier(PlayerStatType statType, float percent, object source)
    {
        _modifiers.Add(new ModifierEntry { StatType = statType, Percent = percent, Source = source });
        Recalculate();
    }

    public void AddModifier(PlayerStatModifier modifier, object source)
    {
        AddModifier(modifier.StatType, modifier.Percent, source);
    }

    // source가 건 보정을 모두 걷는다.
    public void RemoveModifiers(object source)
    {
        bool isRemoved = false;

        for (int i = _modifiers.Count - 1; i >= 0; i--)
        {
            if (ReferenceEquals(_modifiers[i].Source, source))
            {
                _modifiers.RemoveAt(i);
                isRemoved = true;
            }
        }

        if (isRemoved)
        {
            Recalculate();
        }
    }

    // 정수로 표시하는 UI용. 소수점 첫째 자리에서 반올림한다. (반은 올림. 10.5 → 11)
    public static int ToDisplayInt(float value)
    {
        return (int)Math.Round((decimal)value, 0, MidpointRounding.AwayFromZero);
    }

    // 모든 스탯을 다시 계산하고, 값이 바뀐 스탯을 알린다.
    // 알림을 받은 쪽(Changed)이 그 안에서 보정을 다시 걸거나 걷으면, 지금 알림을 모두 마친 뒤에 한 번 더 계산해 알린다.
    // (알리는 도중에 값과 표시 버퍼가 바뀌어 알림이 빠지거나 겹치지 않게 한다)
    private void Recalculate()
    {
        if (_isRecalculating)
        {
            _needsRecalculate = true;
            return;
        }

        _isRecalculating = true;

        try
        {
            do
            {
                _needsRecalculate = false;

                for (int i = 0; i < StatCount; i++)
                {
                    float value = Calculate((PlayerStatType)i);
                    _changedFlags[i] = value != _values[i];
                    _values[i] = value;
                }

                for (int i = 0; i < StatCount; i++)
                {
                    if (_changedFlags[i])
                    {
                        Changed?.Invoke((PlayerStatType)i);
                    }
                }
            }
            while (_needsRecalculate);
        }
        finally
        {
            _isRecalculating = false;
        }
    }

    private float Calculate(PlayerStatType statType)
    {
        decimal baseValue = ToDecimal(_baseStats.GetBaseValue(statType));
        decimal percent = 0m;

        for (int i = 0; i < _modifiers.Count; i++)
        {
            if (_modifiers[i].StatType == statType)
            {
                percent += ToDecimal(_modifiers[i].Percent);
            }
        }

        decimal value = IsPercentPoint(statType) ? baseValue + percent : baseValue * (1m + percent / 100m);
        GetLimits(statType, out decimal min, out decimal max);
        value = Math.Min(max, Math.Max(min, value));
        return (float)Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);
    }

    // 계산용 decimal로 바꾼다. 숫자가 아니거나(NaN) 무한대·지나치게 큰 값은 decimal로 바꿀 수 없어 예외가 나므로 범위 안으로 막는다. (NaN은 0)
    private static decimal ToDecimal(float value)
    {
        if (float.IsNaN(value))
        {
            return 0m;
        }

        return (decimal)Math.Min(MaxInputValue, Math.Max(-MaxInputValue, value));
    }

    // 값 자체가 %인 스탯인지 여부. 이런 스탯은 보정을 기본값에 곱하지 않고 그대로 더한다.
    private static bool IsPercentPoint(PlayerStatType statType)
    {
        return statType == PlayerStatType.CooldownReduction
            || statType == PlayerStatType.CritChance
            || statType == PlayerStatType.LifeSteal;
    }

    // 최종 값이 가질 수 있는 범위. 감소 보정이 지나쳐도 게임이 깨지지 않게 하는 안전 범위다. (기획상의 상한은 정해지지 않았다, GDD 9장)
    private static void GetLimits(PlayerStatType statType, out decimal min, out decimal max)
    {
        min = 0m;
        max = decimal.MaxValue;

        switch (statType)
        {
            case PlayerStatType.MaxHealth:
                // 최대 체력이 0이면 시작하자마자 죽는다.
                min = 1m;
                break;
            case PlayerStatType.AttackSpeed:
                // 0이면 공격 애니메이션이 멈춰 공격 상태가 끝나지 않는다.
                min = 0.1m;
                break;
            case PlayerStatType.CooldownReduction:
            case PlayerStatType.CritChance:
                max = PercentMax;
                break;
        }
    }
}

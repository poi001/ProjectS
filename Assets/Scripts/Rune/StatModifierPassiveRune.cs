using System.Collections.Generic;
using UnityEngine;

// 스탯 보정 패시브 룬. 발동해 있는 동안 정의 데이터(StatModifierPassiveRuneSO)의 % 보정을 플레이어 스탯에 건다. (GDD 4.2, 4.4)
// 보정은 이 룬 자신을 source로 걸고, 해제할 때 한꺼번에 걷는다. (PlayerStats.AddModifier / RemoveModifiers)
public class StatModifierPassiveRune : PassiveRune
{
    private readonly StatModifierPassiveRuneSO _data;
    // 보정을 건 스탯. 플레이어가 다시 Init하면 스탯이 새로 만들어지므로, 걷을 때는 걸었던 그 스탯에서 걷는다.
    private PlayerStats _stats;

    public StatModifierPassiveRune(StatModifierPassiveRuneSO data)
        : base(data)
    {
        _data = data;
    }

    protected override void OnActivated()
    {
        _stats = Owner.Stats;

        if (_stats == null)
        {
            Debug.LogWarning("[StatModifierPassiveRune] 플레이어의 스탯이 아직 없어 보정을 걸지 못했습니다. 룬은 플레이어의 Init이 끝난 뒤에 Init해야 합니다.", Owner);
            return;
        }

        IReadOnlyList<PlayerStatModifier> modifiers = _data.Modifiers;

        for (int i = 0; i < modifiers.Count; i++)
        {
            _stats.AddModifier(modifiers[i], this);
        }
    }

    protected override void OnDeactivated()
    {
        if (_stats == null)
        {
            return;
        }

        _stats.RemoveModifiers(this);
        _stats = null;
    }
}

using System.Collections.Generic;
using UnityEngine;

// 스탯 보정 패시브 룬의 정의 데이터. 장착하고 있는 동안 플레이어 스탯에 % 보정을 건다. (GDD 4.2, 4.4)
// 전투 중 동작은 StatModifierPassiveRune이 맡는다.
[CreateAssetMenu(fileName = "StatModifierPassiveRune", menuName = "SO/Rune/Passive/Stat Modifier")]
public class StatModifierPassiveRuneSO : PassiveRuneDataSO
{
    [Header("스탯 보정")]
    [Tooltip("이 룬이 발동해 있는 동안 플레이어 스탯에 거는 보정입니다. 여러 스탯을 함께 넣을 수 있습니다.\n비워 두면 아무 효과가 없습니다.")]
    [SerializeField] private PlayerStatModifier[] _modifiers = new PlayerStatModifier[0];

    public IReadOnlyList<PlayerStatModifier> Modifiers => _modifiers;

    public override PassiveRune CreateRune()
    {
        return new StatModifierPassiveRune(this);
    }
}

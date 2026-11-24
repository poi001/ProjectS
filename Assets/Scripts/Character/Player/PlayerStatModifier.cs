using System;
using UnityEngine;

// 플레이어 스탯 하나에 거는 보정 값. (GDD 4.2) 증가·감소는 모두 %로 적는다.
// 인스펙터에 적어 두는 데이터용이며(테스트 설정, 추후 패시브 룬·장비 데이터), PlayerStats.AddModifier로 적용한다.
[Serializable]
public struct PlayerStatModifier
{
    [Tooltip("보정할 스탯입니다.")]
    [SerializeField] private PlayerStatType _statType;
    [Tooltip("보정 값입니다. 양수는 증가, 음수는 감소입니다. (단위: %)\n최대체력·공격력·공격속도·이동속도·최대기력은 기본값의 이 %만큼 늘거나 줍니다. (기본 100에 10이면 110)\n재사용 대기시간 감소·치명타 확률·피해 흡수량은 값 자체가 %라서 이 값을 그대로 더합니다. (기본 5%에 10이면 15%)")]
    [SerializeField] private float _percent;

    public PlayerStatType StatType => _statType;
    public float Percent => _percent;

    public PlayerStatModifier(PlayerStatType statType, float percent)
    {
        _statType = statType;
        _percent = percent;
    }
}

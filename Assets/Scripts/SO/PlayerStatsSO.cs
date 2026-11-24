using System;
using UnityEngine;

// 플레이어의 기본 스탯 수치. (GDD 4.1) 공통 스탯(CharacterStatsSO: 공격력, 공격속도, 최대체력)에 플레이어 전용 스탯을 더한다.
// 여기 적는 값은 보정을 적용하기 전의 기본값이다. 룬·장비 등의 % 보정과 반올림은 런타임 스탯(PlayerStats)이 처리한다.
[CreateAssetMenu(fileName = "PlayerStats", menuName = "SO/Player Stats")]
public class PlayerStatsSO : CharacterStatsSO
{
    [Header("이동")]
    [Tooltip("이동속도의 기본값입니다. 땅 위와 공중(점프·낙하 중)의 좌우 이동 속도에 함께 쓰입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0f)] private float _moveSpeed = 7f;
    [Tooltip("공중에서 추가로 점프할 수 있는 횟수입니다. 땅에 닿으면 다시 채워집니다. (0이면 공중 점프 불가)\n% 보정을 받는 스탯이 아니라 동작의 횟수입니다.")]
    [SerializeField, Min(0)] private int _airJumpCount = 1;

    [Header("재사용 대기시간·기력")]
    [Tooltip("재사용 대기시간 감소의 기본값입니다. 대시·블로킹·스킬처럼 재사용 대기시간이 있는 동작의 대기시간을 이 비율만큼 줄입니다. (단위: %, 0이면 줄이지 않음, 100이면 대기시간 없음)")]
    [SerializeField, Range(0f, 100f)] private float _cooldownReduction = 0f;
    [Tooltip("최대 기력의 기본값입니다. 벽타기·대시·공중 점프·블로킹·스킬이 기력을 소모하며, 전투를 시작할 때 가득 찬 채로 시작합니다. 동작별 소모량은 Player 프리팹의 기력 항목에서 정합니다.")]
    [SerializeField, Min(0f)] private float _maxStamina = 100f;
    [Tooltip("초당기력회복력의 기본값입니다. 기력이 1초에 걸쳐 이만큼 연속해서 회복됩니다. (10이면 1초 동안 10이 조금씩 참) 기력을 소모한 직후에는 Player 프리팹의 Stamina Regen Delay 동안 회복이 멈춥니다. (단위: 기력/초, 0이면 회복 없음)")]
    [SerializeField, Min(0f)] private float _staminaRegen = 10f;

    [Header("치명타·피해 흡수")]
    [Tooltip("치명타 확률의 기본값입니다. 일반 공격의 타격마다 이 확률로 치명타가 됩니다. (단위: %, 0이면 치명타 없음, 100이면 항상 치명타)")]
    [SerializeField, Range(0f, 100f)] private float _critChance = 0f;
    [Tooltip("치명타가 터졌을 때 피해에 곱하는 배율입니다. 2면 2배의 피해를 줍니다. % 보정을 받는 스탯이 아닙니다.")]
    [SerializeField, Min(1f)] private float _critDamageMultiplier = 2f;
    [Tooltip("피해 흡수량의 기본값입니다. 일반 공격으로 실제로 입힌 피해 중 이 비율만큼 체력을 회복합니다. (단위: %, 0이면 회복 없음)")]
    [SerializeField, Min(0f)] private float _lifeSteal = 0f;

    [Header("룬 장착 한도")]
    [Tooltip("패시브 룬을 장착할 수 있는 최대 개수입니다. % 보정을 받는 스탯이 아닙니다. (GDD 5.3)\n액티브 룬은 스킬 키 하나로 시전하므로 1개로 고정이며 여기서 정하지 않습니다.")]
    [SerializeField, Min(0)] private int _passiveRuneLimit = 8;

    // 에디터에서 값을 고쳤을 때 호출된다. 플레이 중에 수치를 조정하면 PlayerStats가 받아 최종 스탯을 다시 계산한다. (빌드에서는 호출되지 않는다)
    public event Action Changed;

    public float MoveSpeed => _moveSpeed;
    public int AirJumpCount => _airJumpCount;
    public float CooldownReduction => _cooldownReduction;
    public float MaxStamina => _maxStamina;
    public float StaminaRegen => _staminaRegen;
    public float CritChance => _critChance;
    public float CritDamageMultiplier => _critDamageMultiplier;
    public float LifeSteal => _lifeSteal;
    public int PassiveRuneLimit => _passiveRuneLimit;

    // 스탯 종류로 기본값을 꺼낸다. (PlayerStats가 최종 값을 계산할 때 쓴다)
    public float GetBaseValue(PlayerStatType statType)
    {
        switch (statType)
        {
            case PlayerStatType.MaxHealth:
                return MaxHealth;
            case PlayerStatType.AttackPower:
                return AttackPower;
            case PlayerStatType.AttackSpeed:
                return AttackSpeed;
            case PlayerStatType.MoveSpeed:
                return _moveSpeed;
            case PlayerStatType.CooldownReduction:
                return _cooldownReduction;
            case PlayerStatType.MaxStamina:
                return _maxStamina;
            case PlayerStatType.CritChance:
                return _critChance;
            case PlayerStatType.LifeSteal:
                return _lifeSteal;
            case PlayerStatType.StaminaRegen:
                return _staminaRegen;
            default:
                Debug.LogError($"PlayerStatsSO에 '{statType}' 스탯의 기본값이 없습니다.", this);
                return 0f;
        }
    }

    private void OnValidate()
    {
        Changed?.Invoke();
    }
}

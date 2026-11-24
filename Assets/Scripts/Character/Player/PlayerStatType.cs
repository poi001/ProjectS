// 플레이어 스탯의 종류. (GDD 4.1)
// 스탯을 새로 추가할 때는 여기에 값을 더하고(맨 뒤에, 번호를 이어서), PlayerStatsSO에 기본값 필드와 GetBaseValue 분기,
// PlayerStats의 계산 규칙(IsPercentPoint, GetLimits)을 함께 추가한다.
// 인스펙터에 저장되는 값이므로 이미 쓰는 번호는 바꾸지 않는다. PlayerStats가 번호를 배열 인덱스로 쓰므로 0부터 빈 번호 없이 이어야 한다.
public enum PlayerStatType
{
    // 최대 체력
    MaxHealth = 0,
    // 공격력. 일반 공격의 피해량을 정한다.
    AttackPower = 1,
    // 공격속도. 일반 공격 애니메이션의 재생 속도 배율이다. (1이 기본 속도)
    AttackSpeed = 2,
    // 이동속도. 땅 위와 공중의 좌우 이동 속도다. (단위: 월드 거리/초)
    MoveSpeed = 3,
    // 재사용 대기시간 감소. 대시·블로킹·스킬 등의 재사용 대기시간을 이만큼 줄인다. (단위: %)
    CooldownReduction = 4,
    // 최대 기력
    MaxStamina = 5,
    // 치명타 확률. 일반 공격이 치명타가 될 확률이다. (단위: %)
    CritChance = 6,
    // 피해 흡수량. 일반 공격으로 입힌 피해 중 체력으로 회복하는 비율이다. (단위: %)
    LifeSteal = 7,
    // 초당기력회복력. 기력이 1초에 걸쳐 회복되는 양이다. (단위: 기력/초)
    StaminaRegen = 8
}

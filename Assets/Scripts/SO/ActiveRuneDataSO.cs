using UnityEngine;

// 액티브 룬(스킬이 되는 룬) 정의 데이터의 부모. (GDD 5.2) 공통 정보(RuneDataSO)에 재사용 대기시간과 소모 기력을 더한다.
// 룬 종류(공격 스킬형, 버프형, 소환형 등)마다 이 클래스를 상속한 SO를 만들어, 그 종류에 필요한 수치를 필드로 두고
// CreateRune에서 짝이 되는 ActiveRune(Assets/Scripts/Rune)을 만들어 돌려준다.
// 에셋은 런타임에 바꾸지 않는다. 전투 중에 달라지는 값은 CreateRune으로 만든 ActiveRune이 가진다.
public abstract class ActiveRuneDataSO : RuneDataSO
{
    [Header("액티브")]
    [Tooltip("시전한 뒤 다시 시전할 수 있을 때까지의 재사용 대기시간입니다. 재사용대기시간감소 스탯을 적용하기 전의 값입니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _cooldown;

    [Tooltip("시전할 때 한 번 소모하는 기력입니다. 기력이 이보다 모자라면 시전할 수 없습니다. (단위: 기력)")]
    [SerializeField, Min(0f)] private float _staminaCost = 30f;

    public float Cooldown => _cooldown;
    public float StaminaCost => _staminaCost;

    // 이 룬의 전투 중 인스턴스를 새로 만든다. 장착한 룬을 전투에서 쓰기 시작할 때 한 번 부른다.
    public abstract ActiveRune CreateRune();
}

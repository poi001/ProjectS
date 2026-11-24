using UnityEngine;

// 룬이 속한 갈래. (공격·방어·보조) 액티브 룬과 패시브 룬 모두 하나씩 가진다. 룬 선택 창에서는 갈래 표시(글자·색)를 정한다.
public enum RuneCategory
{
    Attack,
    Defense,
    Support
}

// 룬 한 개의 정의 데이터. 액티브 룬(ActiveRuneDataSO)과 패시브 룬(PassiveRuneDataSO)의 공통 부모다. (GDD 5장)
// 두 종류가 함께 가지는 정보(이름·아이콘·설명·갈래)만 여기에 둔다. 룬을 보여 주기만 하는 UI는 이 타입으로 받으면 종류를 가리지 않는다.
// 직접 에셋으로 만들 수 없다. 룬 종류별 SO(예: StatModifierPassiveRuneSO)를 에셋으로 만들어 보스 처치 보상(EnemyStatsSO)에 연결한다.
// 에셋은 런타임에 바꾸지 않는다. 전투 중의 동작과 상태는 각 SO의 CreateRune이 만드는 런타임 룬(Assets/Scripts/Rune의 Rune)이 맡는다.
public abstract class RuneDataSO : ScriptableObject
{
    [Header("공통")]
    [Tooltip("룬 이름입니다. 룬 선택 창 등에 표시됩니다. 한글 TMP 폰트가 생기기 전까지는 영문으로 적습니다.")]
    [SerializeField] private string _runeName;

    [Tooltip("룬 아이콘입니다. 비워 두면 회색 사각형으로 표시됩니다.")]
    [SerializeField] private Sprite _icon;

    [Tooltip("설명창에 표시할 효과 설명입니다.")]
    [SerializeField, TextArea(2, 5)] private string _description;

    [Tooltip("룬의 갈래입니다. 룬 선택 창에서 갈래 표시의 글자와 색이 바뀝니다. (공격 = 빨강, 방어 = 파랑, 보조 = 초록)")]
    [SerializeField] private RuneCategory _category;

    public string RuneName => _runeName;
    public Sprite Icon => _icon;
    public string Description => _description;
    public RuneCategory Category => _category;
}

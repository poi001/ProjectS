using UnityEngine;

// 룬 선택 창에서 룬이 속한 갈래. 패널의 갈래 표시(글자·색)를 정한다.
public enum RuneCategory
{
    Attack,
    Defense,
    Support
}

// 룬 한 개의 정의 데이터. 보스 처치 보상(EnemyStatsSO의 Reward Runes)으로 연결한다. (GDD 5장)
// 지금은 룬 선택 창에 보여 줄 정보(이름·아이콘·설명·갈래)만 있다. 장착·효과는 룬 시스템을 만들 때 추가한다.
[CreateAssetMenu(fileName = "RuneData", menuName = "SO/Rune Data")]
public class RuneDataSO : ScriptableObject
{
    [Tooltip("룬 선택 창에 표시할 룬 이름입니다. 한글 TMP 폰트가 생기기 전까지는 영문으로 적습니다.")]
    [SerializeField] private string _runeName;

    [Tooltip("룬 선택 창에 표시할 아이콘입니다. 비워 두면 회색 사각형으로 표시됩니다.")]
    [SerializeField] private Sprite _icon;

    [Tooltip("룬 선택 창의 설명창에 표시할 효과 설명입니다.")]
    [SerializeField, TextArea(2, 5)] private string _description;

    [Tooltip("룬의 갈래입니다. 룬 선택 창에서 갈래 표시의 글자와 색이 바뀝니다. (공격 = 빨강, 방어 = 파랑, 보조 = 초록)")]
    [SerializeField] private RuneCategory _category;

    public string RuneName => _runeName;
    public Sprite Icon => _icon;
    public string Description => _description;
    public RuneCategory Category => _category;
}

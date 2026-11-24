using System.Collections.Generic;
using UnityEngine;

// 적(보스)의 기본 스탯 수치와 처치 보상. 스탯은 공통 스탯(CharacterStatsSO)을 쓴다.
// 보스 전용 수치가 필요해지면 여기에 추가한다.
// 처치 보상(GDD 6.6): 보스가 죽으면 Reward Stones에 넣은 결석이 모두 몸에서 뿜어져 나온다. (BattleResultManager가 소환한다)
//  - 결석 프리팹은 종류(리워드 / 액티브 룬 / 패시브 룬)만 정하고 모든 보스가 같은 프리팹을 쓴다. 결석이 실제로 주는 것은 아래 "결석이 주는 것"에 보스마다 적는다.
[CreateAssetMenu(fileName = "EnemyStats", menuName = "SO/Enemy Stats")]
public class EnemyStatsSO : CharacterStatsSO
{
    [Header("처치 보상: 떨구는 결석")]
    [Tooltip("이 보스를 처치하면 몸에서 뿜어져 나오는 결석 프리팹들입니다. 넣은 것이 모두 함께 나오며, 서로 다른 방향으로 흩어집니다.\n리워드 결석(RewardStone_Ability), 액티브 룬 결석(RewardStone_ActiveRune), 패시브 룬 결석(RewardStone_PassiveRune) 중 이 보스가 주는 것을 넣습니다. 결석 프리팹만 넣을 수 있습니다.\n비워 두면 아무것도 나오지 않습니다. 잡몹은 비워 둡니다. (GDD 6.6)")]
    [SerializeField] private List<RewardStone> _rewardStones = new List<RewardStone>();

    [Header("처치 보상: 결석이 주는 것")]
    [Tooltip("리워드 결석을 주우면 해금되는 기본 동작입니다. 여러 개를 함께 고를 수 있습니다. (예: Knight = WallSlide + WallJump)\n리워드 결석을 떨구지 않는 보스는 Nothing으로 둡니다. (GDD 3.6)")]
    [SerializeField] private PlayerAbility _rewardAbility;

    [Tooltip("리워드 결석을 주웠을 때 뜨는 획득 창에 표시할 아이콘입니다. 비워 두면 회색 사각형으로 표시됩니다.")]
    [SerializeField] private Sprite _rewardAbilityIcon;

    [Tooltip("리워드 결석을 주웠을 때 뜨는 획득 창의 설명창에 표시할 글입니다. 한글 TMP 폰트가 생기기 전까지는 영문으로 적습니다.")]
    [SerializeField, TextArea(2, 5)] private string _rewardAbilityDescription;

    [Tooltip("액티브 룬 결석을 주우면 창 없이 바로 얻는 이 보스 고유의 액티브 룬입니다. 액티브 룬 결석을 떨구지 않는 보스는 비워 둡니다. (GDD 5.4)")]
    [SerializeField] private ActiveRuneDataSO _rewardActiveRune;

    [Tooltip("패시브 룬 결석을 주우면 룬 선택 창에 선택지로 나오는 고유 패시브 룬입니다. 3개를 넣습니다. (GDD 5.4)\n비워 두면 BattleResultManager의 임시 룬(Fallback Runes)이 나옵니다.")]
    [SerializeField] private PassiveRuneDataSO[] _rewardRunes = new PassiveRuneDataSO[0];

    public IReadOnlyList<RewardStone> RewardStones => _rewardStones;
    public PlayerAbility RewardAbility => _rewardAbility;
    public Sprite RewardAbilityIcon => _rewardAbilityIcon;
    public string RewardAbilityDescription => _rewardAbilityDescription;
    public ActiveRuneDataSO RewardActiveRune => _rewardActiveRune;
    public IReadOnlyList<PassiveRuneDataSO> RewardRunes => _rewardRunes;
}

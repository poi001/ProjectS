using System.Collections.Generic;
using UnityEngine;

// 적(보스)의 기본 스탯 수치와 처치 보상. 스탯은 공통 스탯(CharacterStatsSO)을 쓴다.
// 보스 전용 수치가 필요해지면 여기에 추가한다.
[CreateAssetMenu(fileName = "EnemyStats", menuName = "SO/Enemy Stats")]
public class EnemyStatsSO : CharacterStatsSO
{
    [Header("처치 보상")]
    [Tooltip("이 보스를 처치하면 룬 선택 창에 선택지로 나오는 고유 룬입니다. 3개를 넣습니다. (GDD 5.4)\n비워 두면 BattleResultManager의 임시 룬(Fallback Runes)이 나옵니다. 잡몹은 비워 둡니다.")]
    [SerializeField] private RuneDataSO[] _rewardRunes = new RuneDataSO[0];

    public IReadOnlyList<RuneDataSO> RewardRunes => _rewardRunes;
}

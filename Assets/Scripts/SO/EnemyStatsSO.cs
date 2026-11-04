using UnityEngine;

// 적(보스)의 기본 스탯 수치. 현재는 공통 스탯(CharacterStatsSO)만 사용한다.
// 보스 전용 수치가 필요해지면 여기에 추가한다.
[CreateAssetMenu(fileName = "EnemyStats", menuName = "SO/Enemy Stats")]
public class EnemyStatsSO : CharacterStatsSO
{
}

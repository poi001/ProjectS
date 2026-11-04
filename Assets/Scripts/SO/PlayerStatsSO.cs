using UnityEngine;

// 플레이어의 기본 스탯 수치. 공통 스탯(CharacterStatsSO)에 플레이어 전용 스탯을 더한다.
// 룬 등에 의한 보정은 추후 런타임 스탯에서 처리한다.
[CreateAssetMenu(fileName = "PlayerStats", menuName = "SO/Player Stats")]
public class PlayerStatsSO : CharacterStatsSO
{
    [SerializeField, Min(0f)] private float _moveSpeed = 7f;

    public float MoveSpeed => _moveSpeed;
}

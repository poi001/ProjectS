using UnityEngine;

// 플레이어의 기본 스탯 수치. 공통 스탯(CharacterStatsSO)에 플레이어 전용 스탯을 더한다.
// 룬 등에 의한 보정은 추후 런타임 스탯에서 처리한다.
[CreateAssetMenu(fileName = "PlayerStats", menuName = "SO/Player Stats")]
public class PlayerStatsSO : CharacterStatsSO
{
    [SerializeField, Min(0f)] private float _moveSpeed = 7f;
    [Tooltip("공중에서 추가로 점프할 수 있는 횟수입니다. 땅에 닿거나 벽을 타면 다시 채워집니다. (0이면 공중 점프 불가)")]
    [SerializeField, Min(0)] private int _airJumpCount = 1;

    public float MoveSpeed => _moveSpeed;
    public int AirJumpCount => _airJumpCount;
}

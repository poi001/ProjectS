using System;
using UnityEngine;

// 공격 판정 범위(박스) 하나. 캐릭터(루트) 위치 기준 오프셋과 크기만 가진 데이터이며, 판정 자체는 AttackHitChecker가 한다.
// 공격 데이터(KnightSwing, PlayerAttack 등) 안에 필드로 넣어 쓴다. 공격마다 자식 오브젝트를 만들 필요가 없다.
[Serializable]
public struct HitboxShape
{
    [Tooltip("캐릭터(루트) 위치 기준 판정 중심의 오프셋입니다. 오른쪽을 볼 때 기준이며, 왼쪽을 보면 x를 뒤집습니다. (단위: 월드 거리)")]
    [SerializeField] private Vector2 _offset;
    [Tooltip("판정 박스의 가로·세로 크기입니다. (단위: 월드 거리)")]
    [SerializeField] private Vector2 _size;

    public Vector2 Offset => _offset;
    public Vector2 Size => _size;

    public HitboxShape(Vector2 offset, Vector2 size)
    {
        _offset = offset;
        _size = size;
    }

    // facingDirection: 캐릭터가 보는 방향 (오른쪽 1, 왼쪽 -1)
    public Vector2 GetCenter(Vector2 ownerPosition, float facingDirection)
    {
        return ownerPosition + new Vector2(_offset.x * facingDirection, _offset.y);
    }
}

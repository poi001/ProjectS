using System;
using UnityEngine;

// SoulKnight 2페이즈 기도 기믹의 목표물. 기둥 하나의 꼭대기에 놓이며, 플레이어가 공격하면 기믹이 끝난다. (Enemy_GDD 6.11)
// 플레이어의 공격 판정에 잡히도록 Enemy 레이어의 트리거 콜라이더와 함께 둔다.
// 한 번 맞으면 Hit를 알리고, 다시 준비(ResetHit)될 때까지 더 맞지 않는다. 체력은 없으며 피해량과 관계없이 한 번에 끝난다.
public class SoulKnightPillarTarget : MonoBehaviour, IDamageable
{
    private bool _isHit;

    // 공격을 맞았을 때 한 번 발생한다.
    public event Action Hit;

    // 다시 맞을 수 있는 상태로 되돌린다. 기둥이 솟아오를 때 호출한다.
    public void ResetHit()
    {
        _isHit = false;
    }

    public bool TakeDamage(DamageInfo damageInfo)
    {
        if (_isHit)
        {
            return false;
        }

        _isHit = true;
        Hit?.Invoke();
        return true;
    }
}

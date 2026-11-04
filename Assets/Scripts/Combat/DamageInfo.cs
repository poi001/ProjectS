using UnityEngine;

// 공격 측이 피격 대상에게 전달하는 타격 정보. 타격마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct DamageInfo
{
    // 데미지 양
    public float Amount;
    // 공격 주체 (누가 때렸는가? - 어그로, 킬 크레딧용)
    public GameObject Instigator;
    // 피격 월드 좌표 (피격 이펙트 생성 위치)
    public Vector2 HitPoint;

    public DamageInfo(float amount, GameObject instigator, Vector2 hitPoint)
    {
        Amount = amount;
        Instigator = instigator;
        HitPoint = hitPoint;
    }
}

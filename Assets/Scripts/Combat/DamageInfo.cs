using UnityEngine;

// 공격 측이 피격 대상에게 전달하는 타격 정보. 타격마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct DamageInfo
{
    // 데미지 양 (치명타라면 치명타 배율까지 곱한 값)
    public float Amount;
    // 공격 주체 (누가 때렸는가? - 어그로, 킬 크레딧용)
    public GameObject Instigator;
    // 피격 월드 좌표 (피격 이펙트 생성 위치)
    public Vector2 HitPoint;
    // 치명타인지 여부. 피해량에는 이미 반영되어 있고, 데미지 숫자 같은 연출을 구분하는 데 쓴다. (지금은 플레이어의 일반 공격만 치명타가 된다)
    public bool IsCritical;

    public DamageInfo(float amount, GameObject instigator, Vector2 hitPoint, bool isCritical = false)
    {
        Amount = amount;
        Instigator = instigator;
        HitPoint = hitPoint;
        IsCritical = isCritical;
    }
}

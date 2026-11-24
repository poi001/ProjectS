using UnityEngine;

// 캐릭터가 피해를 입었을 때 EventBus로 발행하는 이벤트. (데미지 숫자, 타격 FX 같은 연출 시스템이 구독한다)
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct DamageTakenEvent
{
    // 피해를 입은 캐릭터
    public GameObject Target;
    // 공격 주체 (타격 FX의 좌우 방향 판단 등에 쓴다)
    public GameObject Instigator;
    // 실제로 적용된 피해량
    public float Amount;
    // 피격 월드 좌표
    public Vector2 HitPoint;
    // 치명타였는지 여부 (데미지 숫자를 치명타용으로 띄우는 데 쓴다)
    public bool IsCritical;

    public DamageTakenEvent(GameObject target, GameObject instigator, float amount, Vector2 hitPoint, bool isCritical = false)
    {
        Target = target;
        Instigator = instigator;
        Amount = amount;
        HitPoint = hitPoint;
        IsCritical = isCritical;
    }
}

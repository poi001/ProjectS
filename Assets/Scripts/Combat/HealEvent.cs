using UnityEngine;

// 캐릭터가 체력을 회복했을 때 EventBus로 발행하는 이벤트. (회복 숫자 같은 연출 시스템이 구독한다)
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct HealEvent
{
    // 회복한 캐릭터
    public GameObject Target;
    // 실제로 회복된 양
    public float Amount;
    // 회복 표시 월드 좌표
    public Vector2 Position;

    public HealEvent(GameObject target, float amount, Vector2 position)
    {
        Target = target;
        Amount = amount;
        Position = position;
    }
}

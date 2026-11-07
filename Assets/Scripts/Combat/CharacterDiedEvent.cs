using UnityEngine;

// 캐릭터가 죽었을 때(체력이 0이 된 순간) EventBus로 발행하는 이벤트. (KO 연출 같은 연출 시스템이 구독한다)
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct CharacterDiedEvent
{
    // 죽은 캐릭터
    public GameObject Target;

    public CharacterDiedEvent(GameObject target)
    {
        Target = target;
    }
}

using UnityEngine;

// 캐릭터의 사망 애니메이션이 끝났을 때 EventBus로 발행하는 이벤트. (KO 연출을 끝내는 시점 등에 쓴다)
// 죽은 순간의 CharacterDiedEvent와 짝을 이룬다. 캐릭터당 한 번만 발행된다.
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct CharacterDeathFinishedEvent
{
    // 사망 애니메이션이 끝난 캐릭터
    public GameObject Target;

    public CharacterDeathFinishedEvent(GameObject target)
    {
        Target = target;
    }
}

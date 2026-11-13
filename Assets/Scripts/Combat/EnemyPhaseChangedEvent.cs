using UnityEngine;

// 적(보스)의 새 페이즈가 시작된 순간 EventBus로 발행하는 이벤트. (페이즈 표시 UI, 연출 시스템 등이 구독한다)
// 이전 페이즈의 체력을 모두 잃고 전환 연출과 체력 충전까지 끝난 뒤에 발행된다. 전투 시작의 1페이즈에는 발행되지 않는다.
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct EnemyPhaseChangedEvent
{
    // 페이즈가 바뀐 적
    public GameObject Target;
    // 새로 시작된 페이즈 (2부터)
    public int Phase;

    public EnemyPhaseChangedEvent(GameObject target, int phase)
    {
        Target = target;
        Phase = phase;
    }
}

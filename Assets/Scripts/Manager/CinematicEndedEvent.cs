using UnityEngine;

// 시네마틱(연출 장면)이 끝났을 때 EventBus로 발행하는 이벤트. CinematicStartedEvent를 발행한 쪽이 발행한다.
// 구독자는 시작 때 바꿔 둔 것(카메라, 검은 띠, 전투 UI, 플레이어 입력)을 원래대로 되돌린다.
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct CinematicEndedEvent
{
    // 시작 때 카메라가 비추던 대상
    public GameObject FocusTarget;

    public CinematicEndedEvent(GameObject focusTarget)
    {
        FocusTarget = focusTarget;
    }
}

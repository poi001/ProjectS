using UnityEngine;

// 시네마틱(연출 장면)이 시작될 때 EventBus로 발행하는 이벤트. 시네마틱을 여는 쪽(보스의 연출 상태 등)이 발행한다.
// 구독자: CinematicManager(카메라 줌인), CinematicUI(위아래 검은 띠, 전투 UI 숨김), Player(입력 잠금)
// 시작한 쪽은 연출이 끝나거나 중간에 끊길 때 반드시 CinematicEndedEvent를 발행한다.
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct CinematicStartedEvent
{
    // 카메라가 비출 대상. null이면 카메라를 움직이지 않는다.
    public GameObject FocusTarget;

    public CinematicStartedEvent(GameObject focusTarget)
    {
        FocusTarget = focusTarget;
    }
}

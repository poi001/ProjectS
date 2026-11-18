using UnityEngine;

// 시네마틱(연출 장면)이 시작될 때 EventBus로 발행하는 이벤트. 시네마틱을 여는 쪽(보스의 연출 상태 등)이 발행한다.
// 구독자: CinematicManager(카메라 줌인), CinematicUI(위아래 검은 띠, 전투 UI 숨김), Player(입력 잠금)
// 시작한 쪽은 연출이 끝나거나 중간에 끊길 때 반드시 CinematicEndedEvent를 발행한다.
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct CinematicStartedEvent
{
    // 카메라가 비출 대상. null이면 카메라를 움직이지 않는다.
    public GameObject FocusTarget;
    // 카메라 구도를 여는 쪽이 직접 정하는지 여부. 켜져 있으면 대상의 위치와 CinematicManager의 줌 크기 대신 아래 두 값을 쓴다.
    // 이때는 원래 화면 밖이 보이지 않게 하는 제한을 적용하지 않으므로, 여는 쪽이 구도를 책임진다. (Mushroom 반응속도 기믹)
    public bool HasCustomFraming;
    // 직접 정한 카메라 중심 (월드 좌표)
    public Vector2 FramingCenter;
    // 직접 정한 카메라 직교 크기 (화면 세로 절반의 월드 길이). 원래 크기보다 크면 원래 크기를 쓴다.
    public float FramingOrthographicSize;

    public CinematicStartedEvent(GameObject focusTarget)
    {
        FocusTarget = focusTarget;
        HasCustomFraming = false;
        FramingCenter = Vector2.zero;
        FramingOrthographicSize = 0f;
    }

    public CinematicStartedEvent(GameObject focusTarget, Vector2 framingCenter, float framingOrthographicSize)
    {
        FocusTarget = focusTarget;
        HasCustomFraming = true;
        FramingCenter = framingCenter;
        FramingOrthographicSize = framingOrthographicSize;
    }
}

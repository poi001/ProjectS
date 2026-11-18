using UnityEngine;

// 시네마틱(연출 장면)이 끝났을 때 EventBus로 발행하는 이벤트. CinematicStartedEvent를 발행한 쪽이 발행한다.
// 구독자는 시작 때 바꿔 둔 것(카메라, 검은 띠, 전투 UI, 플레이어 입력)을 원래대로 되돌린다.
// 발행마다 생성되므로 GC 할당이 없도록 struct로 정의한다.
public struct CinematicEndedEvent
{
    // 시작 때 카메라가 비추던 대상
    public GameObject FocusTarget;
    // 카메라를 되돌리지 않고 그대로 둘지 여부. 시네마틱 도중에 플레이어나 보스가 죽어 KO 연출(KOEffectManager)이
    // 카메라를 이어받을 때 켠다. KO 연출이 끝나며 카메라를 원래 위치·크기로 되돌린다. 검은 띠·전투 UI·입력 잠금은 평소처럼 되돌린다.
    public bool KeepsCamera;

    public CinematicEndedEvent(GameObject focusTarget)
    {
        FocusTarget = focusTarget;
        KeepsCamera = false;
    }

    public CinematicEndedEvent(GameObject focusTarget, bool keepsCamera)
    {
        FocusTarget = focusTarget;
        KeepsCamera = keepsCamera;
    }
}

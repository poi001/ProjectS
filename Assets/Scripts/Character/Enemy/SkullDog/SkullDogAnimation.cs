using UnityEngine;

// SkullDog 전용 애니메이션. SkullDog 컨트롤러의 고유 상태 이름(해시)을 담당한다. 컨트롤러의 상태 이름과 정확히 같아야 한다.
// 공통 상태(Idle, Dead)의 해시는 EnemyAnimation에 있다.
public class SkullDogAnimation : EnemyAnimation
{
    // 대시 준비 자세(전조). Sprite Sheet_6 한 장을 보여 주는 클립(SkullDog_DashReady)이다. 전조 시간은 클립이 아니라 SkullDog의 Dash Ready Duration이 정한다.
    public static readonly int DashReady = Animator.StringToHash("DashReady");
    // 대시(돌진). Sprite Sheet_7 → 8을 반복하지 않고 한 번 재생한 뒤 마지막 그림(8)을 유지한다.
    public static readonly int Dash = Animator.StringToHash("Dash");
    // 대시 마무리. Sprite Sheet_9 → 10을 반복하지 않고 한 번 재생한 뒤 마지막 그림(10)을 유지한다. 공중에서 끝났다면 이 그림으로 떨어진다.
    public static readonly int DashEnd = Animator.StringToHash("DashEnd");
    // 런. Sprite Sheet_6 ~ 10을 반복 재생한다.
    public static readonly int Run = Animator.StringToHash("Run");
    // 스턴. Sprite Sheet_11 ~ 14를 반복 재생한다.
    public static readonly int Stun = Animator.StringToHash("Stun");
}

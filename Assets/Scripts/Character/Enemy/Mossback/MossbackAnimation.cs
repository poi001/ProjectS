using UnityEngine;

// Mossback 전용 애니메이션. Mossback 컨트롤러의 고유 상태 이름(해시)을 담당한다. 컨트롤러의 상태 이름과 정확히 같아야 한다.
// 공통 상태(Idle, Dead)의 해시는 EnemyAnimation에 있다.
// 스매시와 샷은 같은 클립을 쓰고 재생 속도만 다르다. 스매시 쪽 상태(SmashStart, Smash, SmashEnd)는 컨트롤러 상태의 Speed가 3이고, 샷 쪽 상태는 1이다.
// 스매시 시작(SmashStart)과 샷 준비자세(ShotStart) 상태의 Speed Multiplier는 AttackSpeed 파라미터에 연결되어 있다. (CharacterAnimation.SetAttackSpeed)
public class MossbackAnimation : EnemyAnimation
{
    // 플라이. Enemy3-Fly_0 ~ 7을 반복 재생한다. 좌우로 왔다 갔다 하는 동안 쓴다.
    public static readonly int Fly = Animator.StringToHash("Fly");
    // 스매시 시작. Enemy3-AttackSmashStart_0 ~ 11을 반복하지 않고 한 번 재생한다. 재생 시간이 스매시 전조 시간이다. (3배속)
    public static readonly int SmashStart = Animator.StringToHash("SmashStart");
    // 스매시. Enemy3-AttackSmashLoop_0 ~ 2를 반복 재생한다. 바닥으로 떨어지는 동안 쓴다. (3배속)
    public static readonly int Smash = Animator.StringToHash("Smash");
    // 스매시 끝. Enemy3-SmashEnd_0 ~ 7을 반복하지 않고 한 번 재생한다. 바닥에 내려앉았다가 일어나는 그림이다. (3배속)
    public static readonly int SmashEnd = Animator.StringToHash("SmashEnd");
    // 샷 준비자세. 스매시 시작과 같은 클립을 원래 속도로 한 번 재생한다.
    public static readonly int ShotStart = Animator.StringToHash("ShotStart");
    // 샷 발사. Enemy3-AttackSmashLoop_0 ~ 2를 반복하지 않고 한 번 재생한 뒤 마지막 그림에서 멈춘다. 발사체를 쏘는 동안 쓴다.
    public static readonly int Shot = Animator.StringToHash("Shot");
    // 샷 끝 모션. 스매시 끝과 같은 클립을 원래 속도로 한 번 재생한다.
    public static readonly int ShotEnd = Animator.StringToHash("ShotEnd");
    // 스턴. Enemy3-Hit_0 ~ 3을 반복 재생한다.
    public static readonly int Stun = Animator.StringToHash("Stun");
}

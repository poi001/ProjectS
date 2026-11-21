using UnityEngine;

// MineralGolem 계열(SapphireGolem, GoldGolem) 공용 애니메이션. 컨트롤러의 고유 상태 이름(해시)을 담당한다. 컨트롤러의 상태 이름과 정확히 같아야 한다.
// 원본 컨트롤러는 SapphireGolem.controller이고, 나머지 보스는 그 상태를 그대로 쓰면서 클립만 자기 그림으로 바꾼 Animator Override Controller를 쓴다. (예: GoldGolem.overrideController)
// 공통 상태(Idle, Dead)의 해시는 EnemyAnimation에 있다. (Idle: Golem_1_idle 0 ~ 7 반복, Dead: Golem_1_die 0 ~ 11 한 번 재생 후 마지막 그림에서 멈춤)
public class MineralGolemAnimation : EnemyAnimation
{
    // 이동. Golem_1_walk 0 ~ 9를 반복 재생한다.
    public static readonly int Move = Animator.StringToHash("Move");
    // 공격. Golem_1_attack 0 ~ 10을 한 번 재생한다. 주먹을 내리찍어 바닥에 닿는 그림은 6 ~ 7번이다.
    public static readonly int Attack = Animator.StringToHash("Attack");
    // 스턴. Golem_1_hurt 0 ~ 3을 반복 재생한다.
    public static readonly int Stun = Animator.StringToHash("Stun");
}

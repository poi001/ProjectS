using UnityEngine;

// StoneGolem 전용 애니메이션. StoneGolem 컨트롤러의 고유 상태 이름(해시)을 담당한다. 컨트롤러의 상태 이름과 정확히 같아야 한다.
// 공통 상태(Idle, Dead)의 해시는 EnemyAnimation에 있다.
// Dead는 사망 시트가 없어 스턴 그림(gollux_hit)을 거꾸로 한 번 재생하고 마지막 그림에서 멈추는 클립(StoneGolem_Dead)을 쓴다.
public class StoneGolemAnimation : EnemyAnimation
{
    // 이동. gollux_move 0 ~ 7을 반복 재생한다.
    public static readonly int Move = Animator.StringToHash("Move");
    // 공격. gollux_attack_A 0 ~ 16을 한 번 재생한다. 주먹이 바닥에 닿는 그림은 4 ~ 5번이다.
    public static readonly int Attack = Animator.StringToHash("Attack");
    // 스킬. gollux_attack_B 0 ~ 18을 한 번 재생한다. 크게 내려치는 그림(궤적)은 5 ~ 6번이다.
    public static readonly int Skill = Animator.StringToHash("Skill");
    // 힐. gollux_healing 0 ~ 24를 한 번 재생한다. 몸이 밝게 번쩍이는 그림은 19번이다.
    public static readonly int Heal = Animator.StringToHash("Heal");
    // 스턴. gollux_hit 0 ~ 3을 반복 재생한다.
    public static readonly int Stun = Animator.StringToHash("Stun");
}

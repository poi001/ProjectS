using UnityEngine;

// BloodWing 전용 애니메이션. BloodWing 컨트롤러의 고유 상태 이름(해시)을 담당한다. 컨트롤러의 상태 이름과 정확히 같아야 한다.
// 공통 상태(Idle, Dead)의 해시는 EnemyAnimation에 있다.
// 공격(Attack) 상태의 Speed Multiplier는 AttackSpeed 파라미터에 연결되어 있다. (CharacterAnimation.SetAttackSpeed)
public class BloodWingAnimation : EnemyAnimation
{
    // 브레스(공격) 전조. ATTACK 시트 8장을 0.5초 안에 3번 도는 빠른 클립(BloodWing_AttackReady, 반복 없음)이며, 클립 길이가 전조 시간이다.
    public static readonly int AttackReady = Animator.StringToHash("AttackReady");
    // 브레스(공격). ATTACK_3 한 장을 보여 주는 클립(BloodWing_Attack)으로, 화염을 뿜는 동안 그 그림에 고정한다.
    public static readonly int Attack = Animator.StringToHash("Attack");
    // 대시 준비 자세(전조). FLYING_0 한 장을 보여 주는 클립(BloodWing_DashReady)이다. 전조 시간은 클립이 아니라 BloodWing의 Dash Ready Duration이 정한다.
    public static readonly int DashReady = Animator.StringToHash("DashReady");
    // 대시(돌진)와 복귀. FLYING 시트를 쓰며 반복 재생한다.
    public static readonly int Dash = Animator.StringToHash("Dash");
    // 스킬(메테오). ATTACK 시트 8장을 0.5초 주기로 반복하는 클립(BloodWing_Skill)이다.
    public static readonly int Skill = Animator.StringToHash("Skill");
    // 기절. HURT 시트를 반복하지 않고 한 번 재생한 뒤 마지막 그림을 유지한다.
    public static readonly int Stun = Animator.StringToHash("Stun");
}

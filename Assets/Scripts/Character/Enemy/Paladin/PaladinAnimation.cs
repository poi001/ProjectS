using UnityEngine;

// Paladin 전용 애니메이션. Paladin 컨트롤러의 고유 상태 이름(해시)을 담당한다. 컨트롤러의 상태 이름과 정확히 같아야 한다.
// 공격(Attack1~3) 상태의 Speed Multiplier는 AttackSpeed 파라미터에 연결되어 있다. (CharacterAnimation.SetAttackSpeed)
// 아직 패턴이 없어 아래 상태들은 코드에서 재생하지 않는다. 패턴이 정해지면 상태 코드에서 이 해시로 재생한다.
public class PaladinAnimation : EnemyAnimation
{
    public static readonly int Attack1 = Animator.StringToHash("Attack1");
    public static readonly int Attack2 = Animator.StringToHash("Attack2");
    public static readonly int Attack3 = Animator.StringToHash("Attack3");
    public static readonly int Block = Animator.StringToHash("Block");
    public static readonly int Hurt = Animator.StringToHash("Hurt");
    // 점프는 JumpStart(뛰어오름) → JumpTransition(최고점 전환) → JumpFall(낙하, 반복) 세 단계다.
    public static readonly int JumpStart = Animator.StringToHash("JumpStart");
    public static readonly int JumpTransition = Animator.StringToHash("JumpTransition");
    public static readonly int JumpFall = Animator.StringToHash("JumpFall");
    // 달리기. 반복 재생한다.
    public static readonly int Run = Animator.StringToHash("Run");
}

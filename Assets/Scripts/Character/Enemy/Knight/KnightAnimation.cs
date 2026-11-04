using UnityEngine;

// Knight 전용 애니메이션. Knight 컨트롤러의 고유 패턴 상태 이름(해시)을 담당한다.
// 공격(Attack, AttackCombo) 상태의 Speed Multiplier는 AttackSpeed 파라미터에 연결한다. (CharacterAnimation.SetAttackSpeed)
public class KnightAnimation : EnemyAnimation
{
    public static readonly int Attack = Animator.StringToHash("Attack");
    public static readonly int AttackCombo = Animator.StringToHash("AttackCombo");
    public static readonly int Roll = Animator.StringToHash("Roll");
    public static readonly int Jump = Animator.StringToHash("Jump");
    public static readonly int WallHang = Animator.StringToHash("WallHang");
    public static readonly int SlideStart = Animator.StringToHash("Slide_Start");
    public static readonly int SlideLoop = Animator.StringToHash("Slide_Loop");
    public static readonly int SlideEnd = Animator.StringToHash("Slide_End");
}

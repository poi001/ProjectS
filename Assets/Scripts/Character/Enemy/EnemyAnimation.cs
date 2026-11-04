using UnityEngine;

// 적(보스) 공통 애니메이션. 모든 적 컨트롤러가 가져야 하는 공통 상태 이름(해시)을 담당한다.
// 보스 고유 패턴 애니메이션의 상태 이름(해시)은 보스별 자식 클래스(예: KnightAnimation)에 둔다.
public class EnemyAnimation : CharacterAnimation
{
    public static readonly int Idle = Animator.StringToHash("Idle");
    public static readonly int Dead = Animator.StringToHash("Dead");
}

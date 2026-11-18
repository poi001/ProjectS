using UnityEngine;

// Mushroom 전용 애니메이션. Mushroom 컨트롤러의 고유 상태 이름(해시)을 담당한다. 컨트롤러의 상태 이름과 정확히 같아야 한다.
// 공통 상태(Idle, Dead)의 해시는 EnemyAnimation에 있다.
public class MushroomAnimation : EnemyAnimation
{
    // 발사 클립(Mushroom_Launch)에서 날아가는 그림(Attack_4)이 시작되는 진행도. 그 앞(Attack_0 ~ 3)은 준비 동작이다. (5장 중 5번째)
    public const float LaunchFlyProgress = 0.8f;
    // 점프공격 클립(Mushroom_JumpAttack)에서 벽·땅에 닿은 뒤의 그림(Attack_8 ~ 9)이 시작되는 진행도. 그 앞(Attack_5 ~ 7)은 날아가는 그림이다. (5장 중 4번째)
    public const float JumpAttackImpactProgress = 0.6f;

    // 런. Mushroom-Run_0 ~ 7을 반복 재생한다. 0.25초에 한 바퀴 돈다. 준비 동작(제자리)과 달리는 동안 함께 쓴다.
    public static readonly int Run = Animator.StringToHash("Run");
    // 발사. Mushroom-Attack_0 ~ 4를 반복하지 않고 재생한다. 준비 동작 동안 0 ~ 3(0.5초)을 보여 주고, 날아가는 동안 4를 유지한다. (LaunchFlyProgress)
    public static readonly int Launch = Animator.StringToHash("Launch");
    // 점프. Mushroom-Attack_0 ~ 4를 반복하지 않고 한 번 재생한다. (0.4초) 끝나면 마지막 그림(4)을 유지한 채 솟아오른다.
    public static readonly int Jump = Animator.StringToHash("Jump");
    // 공중점프. 점프와 같은 클립을 2배속으로 재생한다. (0.2초)
    public static readonly int AirJump = Animator.StringToHash("AirJump");
    // 점프공격. Mushroom-Attack_5 ~ 9를 반복하지 않고 재생한다. 날아가는 동안 5 ~ 7을 보여 주고 7을 유지하다가, 벽·땅에 닿으면 8 ~ 9를 재생한다. (JumpAttackImpactProgress)
    public static readonly int JumpAttack = Animator.StringToHash("JumpAttack");
    // 스턴. Mushroom-Hit_0 ~ 4를 반복 재생한다.
    public static readonly int Stun = Animator.StringToHash("Stun");
}

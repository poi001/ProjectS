using UnityEngine;

// 1페이즈 다운(1PhaseDown). 1페이즈의 체력을 모두 잃으면 진행 중인 패턴을 끊고 들어오는 페이즈 전환 연출이다. (Enemy_GDD 1.1, 6.10)
// 제자리에 쓰러져 Phase1Down 애니메이션(crouch_idle 시트, 반복)을 재생하고, 정해진 시간(Phase1DownDuration)이 지나면 공통 체력 충전으로 넘어간다.
// 이 상태에 있는 동안은 시네마틱이다. 시작할 때 시네마틱을 열고(카메라 줌인, 위아래 검은 띠, 전투 UI 숨김, 플레이어 입력 잠금),
// 나갈 때(체력 충전으로 넘어가거나 중간에 끊길 때) 닫는다. 체력 충전은 화면이 원래대로 돌아온 뒤에 보인다.
// 체력이 소진된 상태(CharacterVital.IsDepleted)이므로 이 동안 피해를 입지 않는다.
// 지금은 애니메이션만 재생한다. 연출이 추가되면 이 상태에서 시작하고, 연출이 끝나는 시점에 StartPhaseRefill을 호출하도록 바꾼다.
public class SoulKnightPhase1DownState : SoulKnightState
{
    private float _enterTime;

    public SoulKnightPhase1DownState(SoulKnight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        boss.Movement.SetVelocityX(0f);
        boss.Animation.Play(SoulKnightAnimation.Phase1Down);
        boss.BeginCinematic();
    }

    public override void Exit()
    {
        boss.EndCinematic();
    }

    public override void LogicUpdate()
    {
        if (Time.time - _enterTime >= boss.Phase1DownDuration)
        {
            boss.StartPhaseRefill();
        }
    }

    public override void PhysicsUpdate()
    {
        boss.Movement.SetVelocityX(0f);
    }
}

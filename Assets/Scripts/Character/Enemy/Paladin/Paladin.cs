// Paladin(성기사) 보스. 패턴 기획이 정해지기 전의 최소 뼈대다.
// 지금은 공통 상태(Idle, Dead)만 쓴다. 소환하면 플레이어 쪽을 보고 Idle로 서 있고 체력을 모두 잃으면 죽는다.
// 고유 패턴이 정해지면 CreateStates를 오버라이드해 상태를 추가하고(base.CreateStates()를 먼저 호출), ChangeToNextPattern에서 다음 패턴을 고른다.
public class Paladin : Enemy
{
    // 전투가 시작될 때 플레이어 쪽을 보고 선다. (원본 그림은 오른쪽을 본다)
    protected override void OnInitCompleted()
    {
        base.OnInitCompleted();

        if (!IsTargetDead)
        {
            FaceTarget();
        }
    }

    // Idle 시간이 끝난 뒤 매 프레임 호출된다. (플레이어가 죽어 있는 동안에는 호출되지 않는다)
    // 아직 시전할 패턴이 없으므로 상태를 바꾸지 않고 Idle에 머물며 플레이어 쪽만 계속 바라본다.
    // (Idle로 다시 들어가면 Idle 애니메이션이 처음부터 재생되어 끊겨 보이므로 다시 들어가지 않는다)
    public override void ChangeToNextPattern()
    {
        FaceTarget();
    }
}

// GoldGolem(골드골렘) 보스. MineralGolem 계열이며 모션(Idle, Move, Attack, Stun, Dead)과 그 수치는 MineralGolem에 있다.
// 같은 계열(SapphireGolem)과 모션은 같고, 나가는 스킬과 기믹이 다르다. 스킬·기믹은 아직 정해지지 않아 지금은 MineralGolem의 모션 테스트 순서를 그대로 쓴다.
// 정해지면 이 클래스에 고유 상태와 설정을 추가하고 CreateStates, ChangeToNextPattern을 오버라이드한다. (고유 상태는 GoldGolem/States/에 둔다)
public class GoldGolem : MineralGolem
{
}

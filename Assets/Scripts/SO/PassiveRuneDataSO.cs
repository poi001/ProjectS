// 패시브 룬(평소에 계속 적용되는 룬) 정의 데이터의 부모. (GDD 5.2) 재사용 대기시간이나 발동 키가 없어 공통 정보(RuneDataSO)만 가진다.
// 룬 종류(스탯 보정, 3타마다 추가 피해, 대시 충돌 피해 등)마다 이 클래스를 상속한 SO를 만들어, 그 종류에 필요한 수치를 필드로 두고
// CreateRune에서 짝이 되는 PassiveRune(Assets/Scripts/Rune)을 만들어 돌려준다.
// 에셋은 런타임에 바꾸지 않는다. 전투 중에 달라지는 값(타수 세기 등)은 CreateRune으로 만든 PassiveRune이 가진다.
public abstract class PassiveRuneDataSO : RuneDataSO
{
    // 이 룬의 전투 중 인스턴스를 새로 만든다. 장착한 룬을 전투에서 쓰기 시작할 때 한 번 부른다.
    public abstract PassiveRune CreateRune();
}

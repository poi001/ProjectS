// 효과가 없는 임시 액티브 룬. 시전해도 아무 일도 일어나지 않는다. (스킬 모션, 기력 소모, 재사용 대기시간은 시전하는 쪽인 Player가 처리한다)
// 구체적인 액티브 룬이 기획되기 전까지 시전 흐름을 테스트하는 데 쓴다.
public class PlaceholderActiveRune : ActiveRune
{
    public PlaceholderActiveRune(PlaceholderActiveRuneSO data)
        : base(data)
    {
    }

    protected override void OnCast()
    {
    }
}

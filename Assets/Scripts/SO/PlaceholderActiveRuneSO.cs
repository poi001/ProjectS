using UnityEngine;

// 효과가 없는 임시 액티브 룬의 정의 데이터. 시전하면 스킬 모션이 나오고 기력 소모와 재사용 대기시간만 적용된다.
// 구체적인 액티브 룬이 기획되기 전까지 시전 흐름(장착 → 스킬 키 → 기력·대기시간)을 테스트하는 데 쓴다.
[CreateAssetMenu(fileName = "PlaceholderActiveRune", menuName = "SO/Rune/Active/Placeholder (No Effect)")]
public class PlaceholderActiveRuneSO : ActiveRuneDataSO
{
    public override ActiveRune CreateRune()
    {
        return new PlaceholderActiveRune(this);
    }
}

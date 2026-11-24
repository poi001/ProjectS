using UnityEngine;

// 전투 중의 액티브 룬. 장착한 액티브 룬이 곧 플레이어의 스킬이다. (GDD 3.4, 5.2)
// 룬 종류(공격 스킬형, 버프형, 소환형 등)마다 이 클래스를 상속해 OnCast에 시전 효과를 만든다.
// 시전에 쓸 준비(이벤트 구독 등)가 필요하면 OnInit / OnRelease도 오버라이드한다.
//
// 이 클래스는 "시전하면 무슨 일이 일어나는가"만 맡는다.
// 시전할 수 있는지(재사용 대기시간, 기력, 플레이어의 상태)를 확인하고 기력을 소모하는 것은 시전하는 쪽(Player)이 한다.
// 재사용 대기시간과 소모 기력은 Data(ActiveRuneDataSO)에서 읽는다.
public abstract class ActiveRune : Rune
{
    public new ActiveRuneDataSO Data { get; }

    protected ActiveRune(ActiveRuneDataSO data)
        : base(data)
    {
        Data = data;
    }

    // 스킬을 시전한다. 시전하는 쪽이 시전 조건을 확인한 뒤에 부른다.
    public void Cast()
    {
        if (!IsInitialized)
        {
            Debug.LogWarning($"[ActiveRune] 시작(Init)하지 않은 룬은 시전할 수 없습니다. ({GetType().Name})");
            return;
        }

        OnCast();
    }

    // 시전했을 때의 효과. 시전할 때마다 한 번 호출된다.
    protected abstract void OnCast();
}

using System;
using System.Collections.Generic;
using UnityEngine;

// 전투 중 플레이어가 장착하고 있는 룬. (GDD 5장) 장착 정보(PlayerProfile)의 룬 데이터로 전투 중의 룬(Rune)을 만들어 가진다.
// Player가 하나 가지고 있으며(Player.Runes), Init이 모두 끝난 뒤 Init하고 Release할 때 Release한다.
//  - 패시브 룬: Init하는 순간 모두 발동해 Release할 때까지 적용된다.
//  - 액티브 룬: 하나만 가진다. 언제 시전할지(스킬 키, 재사용 대기시간, 기력)는 Player가 정하고 ActiveRune.Cast를 부른다.
// 전투 도중 룬을 바꾸는 기능은 없다. (기획 미정, GDD 9장) 장착 정보를 바꾸면 다음 Init부터 적용된다.
public class PlayerRunes
{
    private readonly List<PassiveRune> _passiveRunes = new List<PassiveRune>();

    // 장착한 액티브 룬. 장착하지 않았으면 null이다.
    public ActiveRune ActiveRune { get; private set; }
    public IReadOnlyList<PassiveRune> PassiveRunes => _passiveRunes;

    // 장착 정보의 룬으로 전투 중의 룬을 만들고 시작한다. 이전에 만든 룬이 남아 있으면 먼저 끝낸다.
    public void Init(Player owner, PlayerProfile profile)
    {
        Release();

        if (profile.EquippedActiveRune != null)
        {
            ActiveRune rune = profile.EquippedActiveRune.CreateRune();

            if (rune != null)
            {
                rune.Init(owner);
                ActiveRune = rune;
            }
            else
            {
                Debug.LogWarning($"[PlayerRunes] 액티브 룬 '{profile.EquippedActiveRune.name}'이 전투 중의 룬을 만들지 못했습니다. (CreateRune이 null을 반환)", owner);
            }
        }

        IReadOnlyList<PassiveRuneDataSO> passiveRunes = profile.EquippedPassiveRunes;

        for (int i = 0; i < passiveRunes.Count; i++)
        {
            if (passiveRunes[i] == null)
            {
                continue;
            }

            PassiveRune rune = passiveRunes[i].CreateRune();

            if (rune == null)
            {
                Debug.LogWarning($"[PlayerRunes] 패시브 룬 '{passiveRunes[i].name}'이 전투 중의 룬을 만들지 못했습니다. (CreateRune이 null을 반환)", owner);
                continue;
            }

            // 목록에 먼저 넣어, 발동 도중 문제가 생겨도 Release 때 해제되게 한다.
            _passiveRunes.Add(rune);
            rune.Init(owner);
        }
    }

    // 가진 룬을 모두 끝낸다. (패시브 해제 포함) 룬이 없을 때 불러도 안전하다.
    public void Release()
    {
        if (ActiveRune != null)
        {
            ReleaseRune(ActiveRune);
            ActiveRune = null;
        }

        // 발동한 순서의 반대로 해제한다.
        for (int i = _passiveRunes.Count - 1; i >= 0; i--)
        {
            ReleaseRune(_passiveRunes[i]);
        }

        _passiveRunes.Clear();
    }

    // 룬 하나가 끝내는 도중 예외를 내도 나머지 룬은 끝낼 수 있게 한다. (하나 때문에 다른 룬의 효과가 남지 않게)
    private static void ReleaseRune(Rune rune)
    {
        try
        {
            rune.Release();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }
}

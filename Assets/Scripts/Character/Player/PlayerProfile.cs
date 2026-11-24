using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어 정보. 전투 캐릭터(Player)가 아니라 "무엇을 얻었는지"처럼 전투 밖에서도 이어지는 정보를 담는다.
// Player는 전투 씬에서만 소환되므로, 씬이 바뀌어도 유지되고 전투 밖(스토리·상호작용)에서도 읽을 수 있게 따로 둔다.
// 게임 전체에 하나뿐이며 PlayerProfile.Current로 접근한다. 게임을 시작할 때 기본값으로 새로 만들어진다.
// 저장/불러오기는 현재 범위 밖(GDD 8장)이라 게임을 끄면 사라진다. 저장이 생기면 이 클래스를 그대로 직렬화한다.
//
// 담는 정보
// - 동작 해금(GDD 3.6): 대시, 벽타기, 벽 점프, 공중 점프, 블로킹의 획득 여부. 처음에는 모두 없다.
//   Player가 동작을 시작하기 전에 확인하므로, 얻지 못한 동작은 키를 눌러도 아무 일도 일어나지 않는다.
//   보스 처치로 지급하는 흐름은 아직 없어, 지금은 BattleSceneInitialManager의 테스트용 설정으로만 얻는다.
// - 회복 아이템 장착(GDD 3.4): 물약·음식을 장착해야 체력 회복을 쓸 수 있다. 장착 시스템이 없어 지금은 장착 여부만 둔다.
// - 룬 장착(GDD 5.3): 장착한 액티브 룬 하나와 패시브 룬 목록. 처음에는 아무것도 장착하지 않았다.
//   Player가 Init할 때 이 정보로 전투 중의 룬(PlayerRunes)을 만들므로, 전투 도중 바꿔도 그 전투에는 적용되지 않는다.
//   결석으로 얻은 룬을 여기에 넣는 흐름과 보관(장착하지 않은 룬)은 기획 미정(GDD 9장)이라 없다. 지금은 BattleSceneInitialManager의 테스트용 설정으로만 장착한다.
[Serializable]
public class PlayerProfile
{
    [Tooltip("보스를 처치해 얻은 동작입니다. (대시, 벽타기, 벽 점프, 공중 점프, 블로킹) 여러 개를 함께 가질 수 있습니다.")]
    [SerializeField] private PlayerAbility _unlockedAbilities = PlayerAbility.None;

    [Tooltip("체력 회복에 쓰는 회복 아이템(물약·음식)을 장착했는지 여부입니다. 장착하지 않으면 체력 회복을 쓸 수 없습니다.")]
    [SerializeField] private bool _isHealItemEquipped;

    [Tooltip("장착한 액티브 룬입니다. 스킬 키로 시전하는 스킬이 됩니다. 비어 있으면 스킬을 쓸 수 없습니다. (1개 고정)")]
    [SerializeField] private ActiveRuneDataSO _equippedActiveRune;

    [Tooltip("장착한 패시브 룬입니다. 전투를 시작할 때 모두 발동합니다. 장착 한도는 PlayerStatsSO의 Passive Rune Limit입니다.")]
    [SerializeField] private List<PassiveRuneDataSO> _equippedPassiveRunes = new List<PassiveRuneDataSO>();

    public static PlayerProfile Current { get; private set; } = new PlayerProfile();

    public PlayerAbility UnlockedAbilities => _unlockedAbilities;
    public bool IsHealItemEquipped => _isHealItemEquipped;
    public ActiveRuneDataSO EquippedActiveRune => _equippedActiveRune;
    public IReadOnlyList<PassiveRuneDataSO> EquippedPassiveRunes => _equippedPassiveRunes;

    // 여러 동작을 한 번에 물으면 모두 가지고 있어야 true다. None을 물으면 false다.
    public bool HasAbility(PlayerAbility ability)
    {
        return ability != PlayerAbility.None && (_unlockedAbilities & ability) == ability;
    }

    // 이미 가진 동작을 다시 얻어도 그대로 유지된다.
    public void Unlock(PlayerAbility abilities)
    {
        _unlockedAbilities |= abilities;
    }

    // 회복 아이템 장착 상태를 바꾼다. 장착 시스템이 생기면 그쪽에서 호출하고, 회복량 등 아이템 정보도 함께 둔다.
    public void SetHealItemEquipped(bool isEquipped)
    {
        _isHealItemEquipped = isEquipped;
    }

    // 액티브 룬을 장착한다. 액티브 룬은 하나뿐이라 이미 장착한 룬이 있으면 바뀐다. null을 넘기면 장착을 푼다.
    public void EquipActiveRune(ActiveRuneDataSO rune)
    {
        _equippedActiveRune = rune;
    }

    // 패시브 룬을 하나 더 장착한다. 이미 limit개를 장착했으면 장착하지 않고 false를 반환한다.
    // limit: 패시브 룬 장착 한도 (PlayerStatsSO의 Passive Rune Limit)
    // 같은 룬을 여러 번 장착하는 것은 막지 않는다. (중복 장착 가능 여부는 기획 미정, GDD 9장)
    public bool TryEquipPassiveRune(PassiveRuneDataSO rune, int limit)
    {
        if (rune == null || _equippedPassiveRunes.Count >= limit)
        {
            return false;
        }

        _equippedPassiveRunes.Add(rune);
        return true;
    }

    // 장착한 패시브 룬을 모두 푼다.
    public void ClearPassiveRunes()
    {
        _equippedPassiveRunes.Clear();
    }

    // 에디터에서 도메인 리로드를 끄고 플레이해도 이전 플레이의 정보가 남지 않게 시작할 때마다 새로 만든다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCurrent()
    {
        Current = new PlayerProfile();
    }
}

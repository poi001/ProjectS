using System;
using UnityEngine;

// 플레이어 정보. 전투 캐릭터(Player)가 아니라 "무엇을 얻었는지"처럼 전투 밖에서도 이어지는 정보를 담는다.
// Player는 전투 씬에서만 소환되므로, 씬이 바뀌어도 유지되고 전투 밖(스토리·상호작용)에서도 읽을 수 있게 따로 둔다.
// 게임 전체에 하나뿐이며 PlayerProfile.Current로 접근한다. 게임을 시작할 때 기본값으로 새로 만들어진다.
// 저장/불러오기는 현재 범위 밖(GDD 8장)이라 게임을 끄면 사라진다. 저장이 생기면 이 클래스를 그대로 직렬화한다.
//
// 담는 정보
// - 이동 능력 해금(GDD 3.6): 대시, 벽타기, 벽 점프, 공중 점프의 획득 여부.
//   아직 Player가 이 값을 확인하지 않으므로, 지금은 획득 여부와 관계없이 모든 능력을 쓸 수 있다. (해금을 구현할 때 연결한다)
// - 룬(GDD 5장, 예정): 획득한 룬 목록과 액티브·패시브 장착 상태를 이곳에 둔다.
//   보관 한도·중복 획득·교체 시점이 기획 미정(GDD 9장)이라 아직 필드를 만들지 않는다.
[Serializable]
public class PlayerProfile
{
    [Tooltip("보스를 처치해 얻은 이동 능력입니다. (대시, 벽타기, 벽 점프, 공중 점프) 여러 개를 함께 가질 수 있습니다.")]
    [SerializeField] private PlayerAbility _unlockedAbilities = PlayerAbility.None;

    public static PlayerProfile Current { get; private set; } = new PlayerProfile();

    public PlayerAbility UnlockedAbilities => _unlockedAbilities;

    // 여러 능력을 한 번에 물으면 모두 가지고 있어야 true다. None을 물으면 false다.
    public bool HasAbility(PlayerAbility ability)
    {
        return ability != PlayerAbility.None && (_unlockedAbilities & ability) == ability;
    }

    // 이미 가진 능력을 다시 얻어도 그대로 유지된다.
    public void Unlock(PlayerAbility abilities)
    {
        _unlockedAbilities |= abilities;
    }

    // 에디터에서 도메인 리로드를 끄고 플레이해도 이전 플레이의 정보가 남지 않게 시작할 때마다 새로 만든다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCurrent()
    {
        Current = new PlayerProfile();
    }
}

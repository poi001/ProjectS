using System.Collections.Generic;
using UnityEngine;

// 전투가 끝난 뒤의 결과 흐름을 담당한다. (GDD 6.6) BattleManagers에 포함되어 전투 씬에만 존재한다.
// 지금은 보스를 처치한 경우만 처리한다:
//   보스의 사망 애니메이션이 끝남(CharacterDeathFinishedEvent) → 보스 데이터(EnemyStatsSO)의 결석을 모두 몸 가운데에서 소환해 서로 다른 방향으로 뿜어냄
//   → 플레이어가 결석을 주움(RewardStoneCollectedEvent) → 종류에 따라 처리
//     - 리워드 결석: 보스 데이터의 동작을 해금하고 AbilityRewardAcquiredEvent 발행(획득 창이 열림)
//     - 패시브 룬 결석: RuneSelectionRequestedEvent 발행(룬 선택 창이 열림)
//     - 액티브 룬 결석: 창 없이 ActiveRuneAcquiredEvent 발행(룬 시스템이 생기면 받는 쪽이 저장한다)
// 룬 선택 창은 더 이상 보스 처치 뒤에 저절로 열리지 않는다. 패시브 룬 결석을 주워야 열린다.
// 보스가 먼저 죽었다면 플레이어의 승리이므로(GDD 6.4) 따로 승패를 확인하지 않는다. 플레이어가 진 경우의 흐름은 기획 미정이라 없다.
public class BattleResultManager : MonoBehaviour
{
    // 결석 하나가 자기 몫의 방향 구간 가운데에서 벗어날 수 있는 비율. (구간 폭의 절반 대비)
    // 1보다 작게 두어 이웃한 결석과 방향이 붙지 않게 한다. 0.6이면 이웃 결석과 적어도 구간 폭의 40%만큼 벌어진다.
    private const float SectorJitterRatio = 0.6f;

    [Header("결석")]
    [Tooltip("결석이 뿜어져 나오는 방향이 똑바로 위에서 좌우로 기울 수 있는 최대 각도입니다. 이 범위 안에서 무작위로 정해지며, 결석이 여럿이면 범위를 결석 수만큼 나눠 서로 다른 방향으로 흩어집니다.\n0이면 모두 똑바로 위로 솟고, 90이면 옆으로 눕다시피 나올 수 있습니다. (단위: 도)")]
    [SerializeField, Range(0f, 90f)] private float _launchAngleRange = 50f;

    [Header("룬 선택")]
    [Tooltip("보스 데이터(EnemyStatsSO의 Reward Runes)가 비어 있을 때 패시브 룬 결석이 대신 보여 줄 임시 룬입니다. 보스별 룬이 정해지기 전까지 씁니다.")]
    [SerializeField] private PassiveRuneDataSO[] _fallbackRunes = new PassiveRuneDataSO[0];

    // 한 전투에서 결과 흐름은 한 번만 연다.
    private bool _hasStartedResult;
    // 결석을 떨군 보스의 데이터. 결석을 주웠을 때 무엇을 줄지 여기서 읽는다.
    private EnemyStatsSO _rewardSource;

    private void OnEnable()
    {
        EventBus<CharacterDeathFinishedEvent>.Subscribe(OnCharacterDeathFinished);
        EventBus<RewardStoneCollectedEvent>.Subscribe(OnRewardStoneCollected);
    }

    private void OnDisable()
    {
        EventBus<CharacterDeathFinishedEvent>.Unsubscribe(OnCharacterDeathFinished);
        EventBus<RewardStoneCollectedEvent>.Unsubscribe(OnRewardStoneCollected);
    }

    private void OnCharacterDeathFinished(CharacterDeathFinishedEvent finishedEvent)
    {
        if (_hasStartedResult || finishedEvent.Target == null)
        {
            return;
        }

        if (!finishedEvent.Target.TryGetComponent(out Enemy enemy) || !enemy.IsBoss)
        {
            return;
        }

        _hasStartedResult = true;
        _rewardSource = enemy.Stats;
        SpawnRewardStones(enemy);
    }

    // 보스 데이터에 넣어 둔 결석을 모두 보스의 몸 가운데에서 소환하고, 서로 다른 방향으로 뿜어낸다.
    // 씬에 그대로 두므로 씬이 바뀌면 함께 사라진다.
    private void SpawnRewardStones(Enemy boss)
    {
        if (boss.Stats == null)
        {
            return;
        }

        IReadOnlyList<RewardStone> stonePrefabs = boss.Stats.RewardStones;
        int stoneCount = 0;

        for (int i = 0; i < stonePrefabs.Count; i++)
        {
            if (stonePrefabs[i] != null)
            {
                stoneCount++;
            }
        }

        if (stoneCount == 0)
        {
            return;
        }

        // 몸 콜라이더가 꺼져 있으면 bounds가 비어 있으므로 루트 위치를 쓴다.
        Collider2D bodyCollider = boss.BodyCollider;
        Vector3 spawnPosition = bodyCollider != null && bodyCollider.enabled ? bodyCollider.bounds.center : boss.transform.position;

        // 어느 결석이 어느 쪽으로 갈지도 매번 달라지도록 구간 순서를 무작위로 돌린다.
        int sectorOffset = Random.Range(0, stoneCount);
        int spawnedCount = 0;

        for (int i = 0; i < stonePrefabs.Count; i++)
        {
            if (stonePrefabs[i] == null)
            {
                continue;
            }

            int sector = (spawnedCount + sectorOffset) % stoneCount;
            RewardStone stone = Instantiate(stonePrefabs[i], spawnPosition, Quaternion.identity);
            stone.Launch(GetLaunchDirection(sector, stoneCount));
            spawnedCount++;
        }
    }

    // 뿜어져 나갈 방향을 고른다. 전체 범위(위쪽 기준 좌우 Launch Angle Range)를 결석 수만큼 구간으로 나누고,
    // 결석마다 자기 구간 안에서 무작위로 고른다. 구간 가장자리는 쓰지 않으므로 결석끼리 같은 방향이 나오지 않는다.
    private Vector2 GetLaunchDirection(int sector, int sectorCount)
    {
        float sectorWidth = _launchAngleRange * 2f / sectorCount;
        float sectorCenter = -_launchAngleRange + sectorWidth * (sector + 0.5f);
        float jitter = sectorWidth * 0.5f * SectorJitterRatio;
        float angle = (sectorCenter + Random.Range(-jitter, jitter)) * Mathf.Deg2Rad;

        // 각도는 똑바로 위가 0이고 오른쪽으로 기울수록 커진다.
        return new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
    }

    private void OnRewardStoneCollected(RewardStoneCollectedEvent collectedEvent)
    {
        switch (collectedEvent.Type)
        {
            case RewardType.Ability:
                GrantAbility();
                break;
            case RewardType.ActiveRune:
                GrantActiveRune();
                break;
            case RewardType.PassiveRune:
                EventBus<RuneSelectionRequestedEvent>.Publish(new RuneSelectionRequestedEvent(GetRewardRunes()));
                break;
        }
    }

    // 리워드 결석: 보스 데이터의 동작을 해금하고 획득 창을 열게 한다.
    private void GrantAbility()
    {
        PlayerAbility ability = _rewardSource != null ? _rewardSource.RewardAbility : PlayerAbility.None;

        if (ability == PlayerAbility.None)
        {
            Debug.LogWarning("[BattleResultManager] 리워드 결석을 주웠지만 보스 데이터(EnemyStatsSO)의 Reward Ability가 비어 있어 해금할 동작이 없습니다.", this);
        }
        else
        {
            PlayerProfile.Current.Unlock(ability);
        }

        Sprite icon = _rewardSource != null ? _rewardSource.RewardAbilityIcon : null;
        string description = _rewardSource != null ? _rewardSource.RewardAbilityDescription : string.Empty;
        EventBus<AbilityRewardAcquiredEvent>.Publish(new AbilityRewardAcquiredEvent(ability, icon, description));
    }

    // 액티브 룬 결석: 창 없이 바로 얻는다. 룬을 저장·장착하는 쪽은 룬 시스템을 만들 때 이 이벤트를 받는다.
    private void GrantActiveRune()
    {
        ActiveRuneDataSO rune = _rewardSource != null ? _rewardSource.RewardActiveRune : null;

        if (rune == null)
        {
            Debug.LogWarning("[BattleResultManager] 액티브 룬 결석을 주웠지만 보스 데이터(EnemyStatsSO)의 Reward Active Rune이 비어 있습니다.", this);
        }

        EventBus<ActiveRuneAcquiredEvent>.Publish(new ActiveRuneAcquiredEvent(rune));
    }

    // 패시브 룬 결석이 보여 줄 룬. 보스 데이터에 넣어 둔 룬이 하나도 없으면(칸만 있고 비어 있는 경우 포함) 임시 룬을 쓴다.
    private IReadOnlyList<PassiveRuneDataSO> GetRewardRunes()
    {
        if (_rewardSource != null && HasAnyRune(_rewardSource.RewardRunes))
        {
            return _rewardSource.RewardRunes;
        }

        if (_fallbackRunes.Length == 0)
        {
            Debug.LogWarning("[BattleResultManager] 보스 데이터와 임시 룬이 모두 비어 있어 룬 선택 창에 보여 줄 룬이 없습니다.", this);
        }

        return _fallbackRunes;
    }

    private static bool HasAnyRune(IReadOnlyList<PassiveRuneDataSO> runes)
    {
        for (int i = 0; i < runes.Count; i++)
        {
            if (runes[i] != null)
            {
                return true;
            }
        }

        return false;
    }
}

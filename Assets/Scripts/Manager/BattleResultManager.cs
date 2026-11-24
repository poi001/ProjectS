using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 전투가 끝난 뒤의 결과 흐름을 담당한다. (GDD 6.6) BattleManagers에 포함되어 전투 씬에만 존재한다.
// 지금은 보스를 처치한 경우의 룬 선택만 처리한다:
//   보스의 사망 애니메이션이 끝남(CharacterDeathFinishedEvent) → 잠시 대기 → RuneSelectionRequestedEvent 발행(룬 선택 창이 열림)
// 보스가 먼저 죽었다면 플레이어의 승리이므로(GDD 6.4) 따로 승패를 확인하지 않는다. 플레이어가 진 경우의 흐름은 기획 미정이라 없다.
// 동작을 주는 보스의 동작 결정(GDD 6.6)은 아직 없다. 생기면 여기서 함께 연다.
public class BattleResultManager : MonoBehaviour
{
    [Header("룬 선택")]
    [Tooltip("보스의 사망 애니메이션이 끝난 뒤 룬 선택 창을 열기까지 기다리는 시간입니다. 게임 시간 기준이라 일시정지 중에는 흐르지 않습니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _runeSelectDelay = 2f;
    [Tooltip("보스 데이터(EnemyStatsSO의 Reward Runes)가 비어 있을 때 대신 보여 줄 임시 룬입니다. 보스별 룬이 정해지기 전까지 씁니다.")]
    [SerializeField] private RuneDataSO[] _fallbackRunes = new RuneDataSO[0];

    // 한 전투에서 결과 흐름은 한 번만 연다.
    private bool _hasStartedResult;

    private void OnEnable()
    {
        EventBus<CharacterDeathFinishedEvent>.Subscribe(OnCharacterDeathFinished);
    }

    private void OnDisable()
    {
        EventBus<CharacterDeathFinishedEvent>.Unsubscribe(OnCharacterDeathFinished);
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
        RequestRuneSelectionAsync(GetRewardRunes(enemy), destroyCancellationToken).Forget();
    }

    private async UniTaskVoid RequestRuneSelectionAsync(IReadOnlyList<RuneDataSO> runes, CancellationToken cancellationToken)
    {
        // 게임 시간으로 기다린다. (KO 슬로우 모션은 사망 애니메이션이 끝날 때 풀리므로 보통 배속의 시간이다)
        bool isCanceled = await UniTask.Delay(TimeSpan.FromSeconds(_runeSelectDelay), cancellationToken: cancellationToken).SuppressCancellationThrow();

        if (isCanceled)
        {
            return;
        }

        EventBus<RuneSelectionRequestedEvent>.Publish(new RuneSelectionRequestedEvent(runes));
    }

    private IReadOnlyList<RuneDataSO> GetRewardRunes(Enemy boss)
    {
        if (boss.Stats != null && boss.Stats.RewardRunes.Count > 0)
        {
            return boss.Stats.RewardRunes;
        }

        if (_fallbackRunes.Length == 0)
        {
            Debug.LogWarning("[BattleResultManager] 보스 데이터와 임시 룬이 모두 비어 있어 룬 선택 창에 보여 줄 룬이 없습니다.", this);
        }

        return _fallbackRunes;
    }
}

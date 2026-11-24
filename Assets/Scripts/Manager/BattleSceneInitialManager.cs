using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 전투 씬 시작 흐름을 담당한다. BattleManagers에 포함되어 전투 씬에만 존재한다.
// 순서: 다른 전투 매니저 준비 대기 → 전투 구성 받기(테스트용 플레이어 해금 적용) → 캐릭터 생성 → 정해진 순서로 Init(플레이어 → 보스) → 체력바·보스 패턴 게이지 연결 → BattleReadyEvent 발행
// 캐릭터는 Awake/Start에서 스스로 초기화하지 않으므로, 이 흐름이 끝나야 전투가 시작된다.
// 로딩 화면 등은 BattleReadyEvent(또는 IsReady)를 기다렸다가 닫는다.
// 전투 구성(어떤 플레이어·보스인지)은 원래 아웃게임에서 받아오지만, 아직 없으므로 인스펙터에 연결한 프리팹을 임시로 쓴다.
// 소환 위치와 체력바는 씬마다 다르므로, 씬에 놓인 BattleManagers 인스턴스에서 씬 오브젝트를 연결한다.
public class BattleSceneInitialManager : MonoBehaviour
{
    [Header("전투 구성 (임시)")]
    [Tooltip("소환할 플레이어 프리팹입니다. 아웃게임에서 정보를 받아오기 전까지 임시로 씁니다.")]
    [SerializeField] private Player _playerPrefab;

    [Tooltip("소환할 보스 프리팹입니다. 아웃게임에서 정보를 받아오기 전까지 임시로 씁니다.")]
    [SerializeField] private Enemy _bossPrefab;

    [Header("플레이어 해금 (테스트용, 임시)")]
    [Tooltip("전투를 시작할 때 플레이어가 이미 얻은 것으로 칠 동작입니다. 보스 처치로 동작을 주는 흐름이 생기기 전까지 테스트용으로 씁니다.\n기본은 Nothing(모두 잠금, 실제 게임 시작과 같음)입니다. 벽 점프가 필요한 보스 등은 씬의 BattleManagers 인스턴스에서 필요한 동작만 골라 테스트합니다.")]
    [SerializeField] private PlayerAbility _testUnlockedAbilities = PlayerAbility.None;

    [Tooltip("전투를 시작할 때 회복 아이템(물약·음식)을 장착한 것으로 칠지 여부입니다. 장착 시스템이 생기기 전까지 테스트용으로 씁니다.\n끄면(기본) 체력 회복을 쓸 수 없습니다.")]
    [SerializeField] private bool _testHealItemEquipped;

    [Header("소환 위치")]
    [Tooltip("플레이어를 소환할 위치입니다. 씬에 둔 스폰 포인트 오브젝트를 연결합니다.")]
    [SerializeField] private Transform _playerSpawnPoint;

    [Tooltip("보스를 소환할 위치입니다. 씬에 둔 스폰 포인트 오브젝트를 연결합니다.")]
    [SerializeField] private Transform _bossSpawnPoint;

    [Header("UI")]
    [Tooltip("소환한 플레이어의 체력을 표시할 체력바입니다. 비워 두면 연결하지 않습니다.")]
    [SerializeField] private HPBar _playerHPBar;

    [Tooltip("소환한 보스의 체력을 표시할 체력바입니다. 비워 두면 연결하지 않습니다.")]
    [SerializeField] private HPBar _bossHPBar;

    [Tooltip("소환한 보스의 패턴 게이지를 표시할 UI(보스 체력바 아래)입니다. 보스에 패턴 게이지가 없으면 숨깁니다. 비워 두면 연결하지 않습니다.")]
    [SerializeField] private PatternGaugeBar _bossPatternGaugeBar;

    // 전투 준비가 끝났는지 여부. 끝나면 BattleReadyEvent도 발행된다. (이벤트를 놓친 늦은 구독자가 확인하는 용도)
    public bool IsReady { get; private set; }

    private void Start()
    {
        InitializeBattleAsync(destroyCancellationToken).Forget();
    }

    private async UniTaskVoid InitializeBattleAsync(CancellationToken cancellationToken)
    {
        // 같은 프레임에 실행되는 다른 전투 매니저의 Start(PoolManager 프리웜 등)가 모두 끝난 뒤 진행한다.
        // 아웃게임 정보 로드처럼 기다려야 하는 준비가 생기면 이 자리에서 await한다.
        await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);

        if (!CharacterSpawner.HasInstance)
        {
            Debug.LogError("[BattleSceneInitialManager] CharacterSpawner가 없어 캐릭터를 소환할 수 없습니다.", this);
            return;
        }

        if (!ValidateSettings())
        {
            return;
        }

        ApplyTestPlayerProfile();
        CharacterSpawner spawner = CharacterSpawner.Instance;

        // 모두 생성한 뒤 정해진 순서로 Init한다. 보스는 Init할 때 플레이어를 타깃으로 받으므로 플레이어가 먼저다.
        Player player = spawner.Create(_playerPrefab, _playerSpawnPoint.position);
        Enemy boss = spawner.Create(_bossPrefab, _bossSpawnPoint.position);

        spawner.SetContext(new BattleContext(player));
        bool isPlayerReady = spawner.Initialize(player);
        bool isBossReady = spawner.Initialize(boss);

        // Init에 실패한 캐릭터의 체력바는 연결하지 않는다. (실패 원인은 캐릭터가 에러로 알린다)
        if (isPlayerReady && _playerHPBar != null)
        {
            _playerHPBar.Bind(player.Vital);
        }

        if (isBossReady && _bossHPBar != null)
        {
            _bossHPBar.Bind(boss.Vital);
        }

        // 게이지가 없는 보스이거나 Init에 실패했다면 게이지 UI를 숨긴다.
        if (_bossPatternGaugeBar != null)
        {
            _bossPatternGaugeBar.Bind(isBossReady ? boss.PatternGauge : null);
        }

        // 둘 다 준비되어야 전투 준비 완료다. 실패했다면 신호를 보내지 않는다. (원인은 위에서 에러로 알린다)
        if (!isPlayerReady || !isBossReady)
        {
            return;
        }

        IsReady = true;
        EventBus<BattleReadyEvent>.Publish(new BattleReadyEvent(player, boss));
    }

    // 테스트용 해금·장착 값을 플레이어 정보에 넣는다. 이미 얻은 동작은 잃지 않는다. (보스 처치 지급과 장착 시스템이 생기면 없앤다)
    // 플레이어 정보는 플레이를 시작할 때만 새로 만들어지므로, 한 번의 플레이 안에서 전투 씬을 다시 열면 앞에서 넣은 해금이 남는다.
    private void ApplyTestPlayerProfile()
    {
        PlayerProfile profile = PlayerProfile.Current;
        profile.Unlock(_testUnlockedAbilities);

        if (_testHealItemEquipped)
        {
            profile.SetHealItemEquipped(true);
        }
    }

    private bool ValidateSettings()
    {
        bool isValid = true;

        if (_playerPrefab == null || _bossPrefab == null)
        {
            Debug.LogError("[BattleSceneInitialManager] 소환할 플레이어 또는 보스 프리팹이 지정되지 않았습니다.", this);
            isValid = false;
        }

        if (_playerSpawnPoint == null || _bossSpawnPoint == null)
        {
            Debug.LogError("[BattleSceneInitialManager] 플레이어 또는 보스의 소환 위치가 지정되지 않았습니다. 씬의 BattleManagers 인스턴스에서 스폰 포인트를 연결해야 합니다.", this);
            isValid = false;
        }

        return isValid;
    }
}

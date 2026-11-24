using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 전투 씬 시작 흐름을 담당한다. BattleManagers에 포함되어 전투 씬에만 존재한다.
// 순서: 다른 전투 매니저 준비 대기 → 전투 구성 받기(테스트용 플레이어 해금·룬 장착 적용) → 캐릭터 생성 → 정해진 순서로 Init(플레이어 → 보스) → 테스트용 플레이어 스탯 보정 적용 → 체력바·보스 패턴 게이지 연결 → BattleReadyEvent 발행
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

    [Header("플레이어 룬 장착 (테스트용, 임시)")]
    [Tooltip("전투를 시작할 때 장착한 것으로 칠 액티브 룬입니다. 스킬 키(스페이스바)로 시전됩니다. 룬 획득·장착 흐름이 생기기 전까지 테스트용으로 씁니다.\n비워 두면(기본) 플레이어 정보의 장착 상태를 건드리지 않습니다. 처음에는 장착한 룬이 없으므로 스킬이 나가지 않습니다.")]
    [SerializeField] private ActiveRuneDataSO _testActiveRune;

    [Tooltip("전투를 시작할 때 장착한 것으로 칠 패시브 룬입니다. 전투가 시작되자마자 모두 발동합니다. 룬 획득·장착 흐름이 생기기 전까지 테스트용으로 씁니다.\n하나라도 넣으면 플레이어 정보의 패시브 룬을 이 목록으로 바꿉니다. 장착 한도(PlayerStatsSO의 Passive Rune Limit)를 넘는 룬은 장착되지 않습니다.\n비워 두면(기본) 플레이어 정보의 장착 상태를 건드리지 않습니다.")]
    [SerializeField] private PassiveRuneDataSO[] _testPassiveRunes = new PassiveRuneDataSO[0];

    [Header("플레이어 스탯 보정 (테스트용, 임시)")]
    [Tooltip("전투를 시작할 때 플레이어 스탯에 걸어 둘 % 보정입니다. 패시브 룬·장비로 스탯을 보정하는 흐름이 생기기 전까지 테스트용으로 씁니다.\n같은 스탯을 여러 줄 넣으면 합쳐서(합연산) 적용됩니다. 비워 두면(기본) 기본 스탯 그대로입니다.")]
    [SerializeField] private PlayerStatModifier[] _testStatModifiers = new PlayerStatModifier[0];

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

        if (isPlayerReady)
        {
            ApplyTestStatModifiers(player);
        }

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

        ApplyTestRunes(profile);
    }

    // 테스트용 룬을 플레이어 정보에 장착한다. 플레이어가 Init할 때 장착 정보로 룬을 만들므로 소환하기 전에 넣는다.
    // 동작 해금과 달리 더하지 않고 바꾼다. (한 번의 플레이 안에서 전투 씬을 다시 열어도 같은 룬이 겹쳐 장착되지 않게 한다)
    private void ApplyTestRunes(PlayerProfile profile)
    {
        if (_testActiveRune != null)
        {
            profile.EquipActiveRune(_testActiveRune);
        }

        bool hasTestPassiveRune = false;

        for (int i = 0; i < _testPassiveRunes.Length; i++)
        {
            if (_testPassiveRunes[i] != null)
            {
                hasTestPassiveRune = true;
                break;
            }
        }

        if (!hasTestPassiveRune)
        {
            return;
        }

        int limit = _playerPrefab.BaseStats != null ? _playerPrefab.BaseStats.PassiveRuneLimit : 0;
        profile.ClearPassiveRunes();

        for (int i = 0; i < _testPassiveRunes.Length; i++)
        {
            if (_testPassiveRunes[i] == null)
            {
                continue;
            }

            if (!profile.TryEquipPassiveRune(_testPassiveRunes[i], limit))
            {
                Debug.LogWarning($"[BattleSceneInitialManager] 패시브 룬 장착 한도({limit}개)를 넘어 '{_testPassiveRunes[i].name}'은(는) 장착하지 않았습니다.", this);
            }
        }
    }

    // 테스트용 스탯 보정을 소환한 플레이어의 스탯에 건다. 스탯은 Init할 때 새로 만들어지므로 전투마다 한 번씩만 걸린다.
    // 최대 체력이 바뀌어도 체력 비율이 유지되므로 전투는 체력이 가득 찬 채로 시작한다. (룬·장비 보정이 생기면 없앤다)
    private void ApplyTestStatModifiers(Player player)
    {
        for (int i = 0; i < _testStatModifiers.Length; i++)
        {
            player.Stats.AddModifier(_testStatModifiers[i], this);
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

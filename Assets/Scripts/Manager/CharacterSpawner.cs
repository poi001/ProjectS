using System.Collections.Generic;
using UnityEngine;

// 전투 씬의 캐릭터(플레이어, 보스, 잡몹) 소환·제거 창구. BattleManagers에 포함되어 전투 씬에만 존재한다.
// 캐릭터의 초기화와 정리는 ICharacterLifecycle(Init / Release)로 한다.
// 소환은 두 단계로 나뉜다. Create(생성만) → Initialize(Init 호출). 둘을 한 번에 하는 Spawn도 있다.
//  - 전투 시작: BattleSceneInitialManager가 Create로 모두 만든 뒤, 정한 순서대로 Initialize한다.
//  - 전투 중 소환(보스의 잡몹 소환 패턴 등): Spawn을 쓴다.
//  - Init에는 SetContext로 받아 둔 전투 정보(BattleContext)를 넘기므로, Initialize/Spawn 전에 SetContext가 되어 있어야 한다.
// 제거는 Despawn으로 한다. Release한 뒤 오브젝트를 파괴한다.
// 캐릭터는 씬 루트에 생성한다. 씬이 바뀌면 함께 파괴되며, 그때는 Character가 스스로 Release한다.
public class CharacterSpawner : SceneSingleton<CharacterSpawner>
{
    private readonly List<Character> _characters = new List<Character>();
    private BattleContext _context;

    // 이 스포너로 생성해 아직 Despawn하지 않은 캐릭터 목록. (잡몹 전멸 확인, 전투 종료 판정 등에 쓴다)
    // 죽은 캐릭터도 Despawn하기 전까지는 남아 있으므로, 살아 있는 것만 필요하면 Vital.IsDead로 거른다.
    public IReadOnlyList<Character> Characters => _characters;

    public void SetContext(BattleContext context)
    {
        _context = context;
    }

    // 캐릭터를 생성만 하고 Init하지 않는다. Initialize를 호출하기 전까지는 동작하지 않는다.
    public T Create<T>(T prefab, Vector2 position) where T : Character
    {
        if (prefab == null)
        {
            Debug.LogError("[CharacterSpawner] 생성할 캐릭터 프리팹이 null입니다.", this);
            return null;
        }

        T character = Instantiate(prefab, position, Quaternion.identity);
        character.name = prefab.name;
        _characters.Add(character);

        return character;
    }

    // 생성해 둔 캐릭터를 Init한다. 성공하면 true를 반환한다.
    public bool Initialize(Character character)
    {
        if (character == null)
        {
            return false;
        }

        if (_context == null)
        {
            Debug.LogError($"[CharacterSpawner] 전투 정보(BattleContext)가 없어 {character.name}을(를) Init할 수 없습니다. SetContext를 먼저 호출해야 합니다.", this);
            return false;
        }

        character.Init(_context);
        return character.IsInitialized;
    }

    // 생성과 Init을 한 번에 한다. (전투 중 소환용)
    public T Spawn<T>(T prefab, Vector2 position) where T : Character
    {
        T character = Create(prefab, position);
        Initialize(character);

        return character;
    }

    // 캐릭터를 Release한 뒤 파괴한다.
    public void Despawn(Character character)
    {
        if (character == null)
        {
            return;
        }

        _characters.Remove(character);
        character.Release();
        Destroy(character.gameObject);
    }
}

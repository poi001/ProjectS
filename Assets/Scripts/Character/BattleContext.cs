// 캐릭터가 Init할 때 넘겨받는 전투 정보. BattleSceneInitialManager가 만들어 CharacterSpawner에 넘긴다.
// 전투 중에 소환되는 잡몹도 같은 정보로 초기화된다. 캐릭터가 알아야 할 전투 정보가 늘어나면 여기에 추가한다.
public class BattleContext
{
    // 적이 타깃으로 삼는 플레이어
    public Player Player { get; }

    public BattleContext(Player player)
    {
        Player = player;
    }
}

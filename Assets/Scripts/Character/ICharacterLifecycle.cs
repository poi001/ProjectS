// 전투 씬에 소환되는 모든 캐릭터(플레이어, 보스, 잡몹)의 생명주기. Character가 구현한다.
// 캐릭터는 Awake/Start에서 스스로 초기화하지 않고, 소환을 맡은 쪽(CharacterSpawner)이 Init을 호출해야 동작을 시작한다.
// 그래서 초기화 순서를 소환하는 쪽이 정할 수 있다. (예: 플레이어 → 보스)
// 전투에서 빠질 때는 Release로 정리한 뒤 소환한 쪽이 오브젝트를 파괴한다.
public interface ICharacterLifecycle
{
    // 스탯 적용, 상태 생성, 이벤트 구독, 상태기계 시작까지 마친다. 한 번만 호출한다.
    void Init(BattleContext context);

    // Init에서 구독한 이벤트를 해제한다. 오브젝트 파괴는 하지 않는다. 여러 번 호출해도 안전하다.
    void Release();
}

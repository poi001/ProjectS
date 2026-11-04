using UnityEngine;

// 게임 전체 흐름을 담당하는 매니저. 게임 시작 시 자동으로 생성되고 씬이 바뀌어도 유지된다.
// 어느 씬에서 플레이를 시작해도 존재하도록 첫 씬 로드 전에 생성한다.
public class GameManager : PersistentSingleton<GameManager>
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
        if (HasInstance)
        {
            return;
        }

        GameObject managerObject = new GameObject(nameof(GameManager));
        managerObject.AddComponent<GameManager>();
    }
}

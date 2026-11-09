using UnityEngine;
using UnityEngine.InputSystem;

// 게임 전체 흐름을 담당하는 매니저. 게임 시작 시 자동으로 생성되고 씬이 바뀌어도 유지된다.
// 어느 씬에서 플레이를 시작해도 존재하도록 첫 씬 로드 전에 생성한다.
public class GameManager : PersistentSingleton<GameManager>
{
    private void Update()
    {
        // 정식 종료 메뉴가 생기기 전까지 사용하는 임시 단축키.
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

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

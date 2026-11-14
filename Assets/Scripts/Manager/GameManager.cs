using MoreMountains.Feedbacks;
using UnityEngine;

// 게임 전체 흐름을 담당하는 매니저. 게임 시작 시 자동으로 생성되고 씬이 바뀌어도 유지된다.
// 어느 씬에서 플레이를 시작해도 존재하도록 첫 씬 로드 전에 생성한다.
// 일시정지(SetPaused): 게임 시간을 멈추고(Time.timeScale 0) GamePauseChangedEvent를 발행한다. 옵션 창 등 여는 쪽이 켜고 끈다.
public class GameManager : PersistentSingleton<GameManager>
{
    // MMTimeManager가 없는 씬에서 일시정지를 풀 때 되돌릴 배속
    private float _timeScaleBeforePause = 1f;

    public bool IsPaused { get; private set; }

    // 일시정지를 켜거나 끈다. 끌 때는 슬로우 모션 등 진행 중이던 배속으로 되돌린다.
    public void SetPaused(bool isPaused)
    {
        if (IsPaused == isPaused)
        {
            return;
        }

        IsPaused = isPaused;

        if (isPaused)
        {
            _timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
        }
        else
        {
            // 슬로우 모션(피격, KO)은 실제 시간으로 흘러 일시정지 중에 끝났을 수 있다.
            // 멈추기 전 값이 아니라 MMTimeManager가 지금 목표로 하는 배속으로 되돌린다.
            // (CurrentTimeScale은 배속을 서서히 바꾸는 중이면 일시정지 때문에 0 근처 값이 남아 있을 수 있어 쓰지 않는다)
            Time.timeScale = MMTimeManager.HasInstance ? MMTimeManager.Current.TargetTimeScale : _timeScaleBeforePause;
        }

        EventBus<GamePauseChangedEvent>.Publish(new GamePauseChangedEvent(isPaused));
    }

    // 일시정지 중에 슬로우 모션이 시작되거나 끝나면 MMTimeManager(Update)가 배속을 다시 적용한다.
    // 그 뒤(LateUpdate)에 다시 0으로 눌러 두어 창이 열린 채로 게임이 흐르지 않게 한다.
    private void LateUpdate()
    {
        if (IsPaused && Time.timeScale != 0f)
        {
            Time.timeScale = 0f;
        }
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

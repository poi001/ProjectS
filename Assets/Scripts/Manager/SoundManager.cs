using System.Collections.Generic;
using MoreMountains.Tools;
using UnityEngine;

// 볼륨을 조절하는 사운드 채널. MMSoundManager의 믹서 트랙에 대응한다.
public enum SoundChannel
{
    Master,
    Bgm,
    Sfx,
    Ui
}

// 게임 사운드 재생 창구. 게임 코드는 MMSoundManager(Feel)를 직접 부르지 말고 이 클래스를 통해 재생한다.
// 실제 재생, AudioSource 풀링, 믹서 트랙(Master/Music/Sfx/UI) 볼륨은 MMSoundManager가 담당한다.
// Feel 피드백(MMF_Player)의 사운드도 같은 MMSoundManager로 재생되므로, 볼륨 설정이 한 곳에서 관리된다.
// 믹서 설정(settingsSo)이 연결된 MMSoundManager가 함께 있어야 한다. (같은 오브젝트에 붙여 두는 것을 권장)
// MMSoundManager.Instance는 인스턴스가 없으면 설정 없이 자동 생성하므로 쓰지 않고, HasInstance/Current로 확인한다.
// 2D 게임이므로 모든 소리는 2D(spatialBlend 0)로 재생하며, 재생 위치는 의미가 없다.
// 어느 씬에서 플레이를 시작해도 존재하도록, 첫 씬 로드 전에 Resources의 프리팹(SoundManager + MMSoundManager)을 생성한다.
public class SoundManager : PersistentSingleton<SoundManager>
{
    // Resources 폴더 기준 프리팹 경로 (Assets/Resources/Manager/Sound/SoundManager.prefab)
    private const string PrefabResourcePath = "Manager/Sound/SoundManager";

    [Tooltip("배경음악을 바꾸거나 멈출 때 페이드 시간을 따로 지정하지 않았을 때 쓰는 기본 페이드 시간입니다. (단위: 초)\n0 = 페이드 없이 바로 바꿉니다.")]
    [SerializeField, Min(0f)] private float _defaultBgmFadeDuration = 1f;

    // 사운드 데이터별 마지막 재생 시각 (SoundDataSO.MinInterval 판정용, 실제 시간 기준)
    private readonly Dictionary<SoundDataSO, float> _lastPlayTimes = new Dictionary<SoundDataSO, float>();
    private SoundDataSO _currentBgm;
    private AudioSource _bgmSource;

    public SoundDataSO CurrentBgm => _currentBgm;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void CreateInstance()
    {
        if (HasInstance)
        {
            return;
        }

        GameObject prefab = Resources.Load<GameObject>(PrefabResourcePath);

        if (prefab == null)
        {
            Debug.LogError($"[SoundManager] Resources/{PrefabResourcePath} 프리팹을 찾을 수 없어 SoundManager를 생성하지 못했습니다.");
            return;
        }

        GameObject managerObject = Instantiate(prefab);
        managerObject.name = prefab.name;
    }

    private void Start()
    {
        if (!IsReady())
        {
            Debug.LogError("[SoundManager] 믹서 설정(settingsSo)이 연결된 MMSoundManager가 없어 사운드를 재생할 수 없습니다.", this);
        }
    }

    public void PlaySfx(SoundDataSO soundData)
    {
        PlayOneShot(soundData, MMSoundManager.MMSoundManagerTracks.Sfx);
    }

    public void PlayUi(SoundDataSO soundData)
    {
        PlayOneShot(soundData, MMSoundManager.MMSoundManagerTracks.UI);
    }

    // 배경음악을 바꾼다. 재생 중인 곡은 페이드 아웃하고 새 곡은 페이드 인한다. (크로스페이드)
    // 같은 곡이 이미 재생 중이면 처음부터 다시 틀지 않는다.
    public void PlayBgm(SoundDataSO bgmData)
    {
        PlayBgm(bgmData, _defaultBgmFadeDuration);
    }

    public void PlayBgm(SoundDataSO bgmData, float fadeDuration)
    {
        if (bgmData == null || !IsReady())
        {
            return;
        }

        if (bgmData == _currentBgm && _bgmSource != null && _bgmSource.isPlaying)
        {
            return;
        }

        AudioClip clip = bgmData.GetRandomClip();

        if (clip == null)
        {
            Debug.LogWarning($"[SoundManager] 배경음악 데이터에 클립이 없습니다: {bgmData.name}", bgmData);
            return;
        }

        FadeOutCurrentBgm(fadeDuration);

        // persistent: 씬이 바뀌어도 MMSoundManager가 자동으로 정리하지 않는다. (곡 전환은 이 클래스가 직접 관리한다)
        _bgmSource = MMSoundManager.Current.PlaySound(clip, MMSoundManager.MMSoundManagerTracks.Music, Vector3.zero,
            loop: true, volume: bgmData.Volume,
            fade: fadeDuration > 0f, fadeInitialVolume: 0f, fadeDuration: fadeDuration,
            persistent: true, pitch: bgmData.GetRandomPitch());
        _currentBgm = bgmData;
    }

    public void StopBgm()
    {
        StopBgm(_defaultBgmFadeDuration);
    }

    public void StopBgm(float fadeDuration)
    {
        if (!IsReady())
        {
            return;
        }

        FadeOutCurrentBgm(fadeDuration);
    }

    // volume: 0~1
    public void SetVolume(SoundChannel channel, float volume)
    {
        if (!IsReady())
        {
            return;
        }

        MMSoundManager.Current.SetTrackVolume(ToTrack(channel), Mathf.Clamp01(volume));
    }

    public float GetVolume(SoundChannel channel)
    {
        if (!IsReady())
        {
            return 0f;
        }

        return MMSoundManager.Current.GetTrackVolume(ToTrack(channel), false);
    }

    private void PlayOneShot(SoundDataSO soundData, MMSoundManager.MMSoundManagerTracks track)
    {
        if (soundData == null || !IsReady())
        {
            return;
        }

        AudioClip clip = soundData.GetRandomClip();

        if (clip == null || !TryConsumeInterval(soundData))
        {
            return;
        }

        MMSoundManager.Current.PlaySound(clip, track, Vector3.zero,
            volume: soundData.Volume, pitch: soundData.GetRandomPitch());
    }

    // 최소 재생 간격이 지났으면 재생 시각을 기록하고 true를 반환한다.
    // 히트스톱 등으로 timeScale이 0이 되어도 판정되도록 실제 시간(unscaledTime)을 쓴다.
    private bool TryConsumeInterval(SoundDataSO soundData)
    {
        if (soundData.MinInterval <= 0f)
        {
            return true;
        }

        float now = Time.unscaledTime;

        if (_lastPlayTimes.TryGetValue(soundData, out float lastPlayTime) && now - lastPlayTime < soundData.MinInterval)
        {
            return false;
        }

        _lastPlayTimes[soundData] = now;
        return true;
    }

    private void FadeOutCurrentBgm(float fadeDuration)
    {
        AudioSource source = _bgmSource;
        _bgmSource = null;
        _currentBgm = null;

        if (source == null)
        {
            return;
        }

        MMSoundManager soundManager = MMSoundManager.Current;

        // 페이드 인 도중이면 그 페이드가 볼륨을 계속 덮어쓰지 않도록 먼저 멈춘다.
        soundManager.StopFadeSound(source);

        if (fadeDuration > 0f)
        {
            // 페이드가 끝나면 AudioSource를 풀에 반환한다.
            soundManager.FadeSound(source, fadeDuration, source.volume, 0f, null, true);
        }
        else
        {
            soundManager.FreeSound(source);
        }
    }

    private static bool IsReady()
    {
        return MMSoundManager.HasInstance && MMSoundManager.Current.settingsSo != null;
    }

    private static MMSoundManager.MMSoundManagerTracks ToTrack(SoundChannel channel)
    {
        switch (channel)
        {
            case SoundChannel.Bgm:
                return MMSoundManager.MMSoundManagerTracks.Music;
            case SoundChannel.Sfx:
                return MMSoundManager.MMSoundManagerTracks.Sfx;
            case SoundChannel.Ui:
                return MMSoundManager.MMSoundManagerTracks.UI;
            default:
                return MMSoundManager.MMSoundManagerTracks.Master;
        }
    }
}

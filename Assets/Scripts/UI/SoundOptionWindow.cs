using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// 사운드 옵션 창 (임시 그레이박스). BattleUI 프리팹에 중첩해 둔다.
//  - ESC로 열고 닫는다. 오른쪽 위 X 버튼으로도 닫는다.
//  - 슬라이더 3개(전체, BGM, SFX)와 전체 음소거 버튼이 SoundManager의 볼륨·음소거를 바꾼다.
//  - 열려 있는 동안 게임을 일시정지한다. (GameManager.SetPaused → 플레이어 입력 잠금)
// ESC 입력을 받아야 하므로 이 오브젝트는 항상 켜 두고, 창 내용(Content)만 켜고 끈다.
// 볼륨 값은 저장하지 않는다. SoundManager가 씬이 바뀌어도 유지되므로 게임을 끄기 전까지만 이어진다.
public class SoundOptionWindow : MonoBehaviour
{
    [Header("창")]
    [Tooltip("창을 열 때 켜고 닫을 때 끄는 오브젝트입니다. 화면을 덮는 어두운 배경과 창 전체를 담은 Content를 연결합니다.")]
    [SerializeField] private GameObject _content;
    [Tooltip("창을 닫는 X 버튼입니다. (창 오른쪽 위)")]
    [SerializeField] private Button _closeButton;

    [Header("볼륨 슬라이더")]
    [Tooltip("전체 사운드 볼륨 슬라이더입니다. 값 범위는 0~1입니다. (0 = 무음, 1 = 최대)")]
    [SerializeField] private Slider _masterSlider;
    [Tooltip("배경음악(BGM) 볼륨 슬라이더입니다. 값 범위는 0~1입니다. (0 = 무음, 1 = 최대)")]
    [SerializeField] private Slider _bgmSlider;
    [Tooltip("효과음(SFX) 볼륨 슬라이더입니다. 값 범위는 0~1입니다. (0 = 무음, 1 = 최대)")]
    [SerializeField] private Slider _sfxSlider;

    [Header("음소거")]
    [Tooltip("전체 사운드를 끄고 켜는 버튼입니다. 누를 때마다 바뀌며, 슬라이더 값은 그대로 유지됩니다.")]
    [SerializeField] private Button _muteButton;
    [Tooltip("음소거 버튼 안의 글자입니다. 음소거 상태에 따라 아래 두 문구 중 하나로 바뀝니다.")]
    [SerializeField] private TMP_Text _muteLabel;
    [Tooltip("소리가 꺼져 있을 때(음소거 중) 버튼에 표시할 문구입니다.")]
    [SerializeField] private string _mutedText = "MUTE : ON";
    [Tooltip("소리가 켜져 있을 때(음소거 아님) 버튼에 표시할 문구입니다.")]
    [SerializeField] private string _unmutedText = "MUTE : OFF";

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (_content != null)
        {
            _content.SetActive(false);
        }
    }

    private void OnEnable()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.AddListener(Close);
        }

        if (_muteButton != null)
        {
            _muteButton.onClick.AddListener(OnMuteButtonClicked);
        }

        if (_masterSlider != null)
        {
            _masterSlider.onValueChanged.AddListener(OnMasterSliderChanged);
        }

        if (_bgmSlider != null)
        {
            _bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
        }

        if (_sfxSlider != null)
        {
            _sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
        }
    }

    private void OnDisable()
    {
        if (_closeButton != null)
        {
            _closeButton.onClick.RemoveListener(Close);
        }

        if (_muteButton != null)
        {
            _muteButton.onClick.RemoveListener(OnMuteButtonClicked);
        }

        if (_masterSlider != null)
        {
            _masterSlider.onValueChanged.RemoveListener(OnMasterSliderChanged);
        }

        if (_bgmSlider != null)
        {
            _bgmSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);
        }

        if (_sfxSlider != null)
        {
            _sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
        }

        // 창이 열린 채로 꺼지면(씬 전환 등) 일시정지가 남지 않도록 닫는다.
        Close();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
        {
            return;
        }

        if (IsOpen)
        {
            Close();
        }
        else
        {
            Open();
        }
    }

    public void Open()
    {
        if (IsOpen)
        {
            return;
        }

        IsOpen = true;
        RefreshControls();

        if (_content != null)
        {
            _content.SetActive(true);
        }

        if (GameManager.HasInstance)
        {
            GameManager.Instance.SetPaused(true);
        }
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        IsOpen = false;

        if (_content != null)
        {
            _content.SetActive(false);
        }

        if (GameManager.HasInstance)
        {
            GameManager.Instance.SetPaused(false);
        }
    }

    // 슬라이더와 음소거 버튼을 SoundManager의 현재 값에 맞춘다. 값만 맞추고 볼륨 변경 콜백은 부르지 않는다.
    private void RefreshControls()
    {
        if (!SoundManager.HasInstance)
        {
            return;
        }

        SoundManager soundManager = SoundManager.Instance;
        SetSliderValue(_masterSlider, soundManager.GetVolume(SoundChannel.Master));
        SetSliderValue(_bgmSlider, soundManager.GetVolume(SoundChannel.Bgm));
        SetSliderValue(_sfxSlider, soundManager.GetVolume(SoundChannel.Sfx));
        RefreshMuteLabel();
    }

    private void RefreshMuteLabel()
    {
        if (_muteLabel == null)
        {
            return;
        }

        bool isMuted = SoundManager.HasInstance && SoundManager.Instance.IsMuted;
        _muteLabel.text = isMuted ? _mutedText : _unmutedText;
    }

    private void OnMasterSliderChanged(float value)
    {
        SetVolume(SoundChannel.Master, value);
    }

    private void OnBgmSliderChanged(float value)
    {
        SetVolume(SoundChannel.Bgm, value);
    }

    private void OnSfxSliderChanged(float value)
    {
        SetVolume(SoundChannel.Sfx, value);
    }

    private void OnMuteButtonClicked()
    {
        if (!SoundManager.HasInstance)
        {
            return;
        }

        SoundManager soundManager = SoundManager.Instance;
        soundManager.SetMuted(!soundManager.IsMuted);
        RefreshMuteLabel();
    }

    private static void SetVolume(SoundChannel channel, float volume)
    {
        if (SoundManager.HasInstance)
        {
            SoundManager.Instance.SetVolume(channel, volume);
        }
    }

    private static void SetSliderValue(Slider slider, float value)
    {
        if (slider != null)
        {
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));
        }
    }
}

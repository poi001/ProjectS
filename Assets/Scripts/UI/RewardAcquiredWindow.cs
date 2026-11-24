using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 결석을 주웠을 때 뜨는 획득 창 (임시 그레이박스, GDD 6.6). BattleUI 프리팹에 중첩해 둔다.
//  - RewardStoneCollectedEvent를 받으면 패널을 채우고 연다. 패널은 룬 선택 창의 패널 하나와 같은 모양이다. (아이콘 → 종류 팻말 → 설명창)
//  - 닫기는 Content 아래의 UIClosePrompt가 맡는다. (Z키 또는 화면 클릭, 깜빡이는 안내 글)
//  - 열린 동안 게임을 일시정지한다. (UIWindow의 Pauses Game)
// 실제 획득 처리(동작 해금, 룬 지급)는 아직 없다. 지금은 주운 것을 보여 주기만 한다.
public class RewardAcquiredWindow : UIWindow
{
    [Header("참조")]
    [Tooltip("보상 아이콘 이미지입니다. 결석에 아이콘이 없으면 이 이미지의 색(회색)으로만 보입니다.")]
    [SerializeField] private Image _iconImage;
    [Tooltip("종류 팻말의 배경 이미지입니다. 보상 종류에 따라 아래 색으로 바뀝니다.")]
    [SerializeField] private Image _typeBackground;
    [Tooltip("종류 팻말의 글자입니다. 보상 종류에 따라 아래 문구로 바뀝니다.")]
    [SerializeField] private TMP_Text _typeText;
    [Tooltip("보상 설명 글자입니다.")]
    [SerializeField] private TMP_Text _descriptionText;

    [Header("종류 팻말")]
    [Tooltip("기본 동작의 팻말 문구입니다. 한글 TMP 폰트가 생기기 전까지는 영문으로 둡니다.")]
    [SerializeField] private string _abilityLabel = "ABILITY";
    [Tooltip("기본 동작의 팻말 색입니다. (결석 그림과 같은 금색 계열)")]
    [SerializeField] private Color _abilityColor = new Color(0.8f, 0.6f, 0.15f, 1f);
    [Tooltip("액티브 룬의 팻말 문구입니다.")]
    [SerializeField] private string _activeRuneLabel = "ACTIVE RUNE";
    [Tooltip("액티브 룬의 팻말 색입니다. (결석 그림과 같은 보라색 계열)")]
    [SerializeField] private Color _activeRuneColor = new Color(0.55f, 0.3f, 0.85f, 1f);
    [Tooltip("패시브 룬의 팻말 문구입니다.")]
    [SerializeField] private string _passiveRuneLabel = "PASSIVE RUNE";
    [Tooltip("패시브 룬의 팻말 색입니다. (결석 그림과 같은 청록색 계열)")]
    [SerializeField] private Color _passiveRuneColor = new Color(0.1f, 0.6f, 0.65f, 1f);

    private void OnEnable()
    {
        EventBus<RewardStoneCollectedEvent>.Subscribe(OnRewardStoneCollected);
    }

    protected override void OnDisable()
    {
        EventBus<RewardStoneCollectedEvent>.Unsubscribe(OnRewardStoneCollected);
        base.OnDisable();
    }

    // 주운 보상으로 패널을 채우고 창을 연다. 이미 열려 있으면 무시한다.
    public void Show(RewardType type, Sprite icon, string description)
    {
        if (IsOpen)
        {
            return;
        }

        if (_iconImage != null)
        {
            _iconImage.sprite = icon;
        }

        if (_typeText != null)
        {
            _typeText.text = GetTypeLabel(type);
        }

        if (_typeBackground != null)
        {
            _typeBackground.color = GetTypeColor(type);
        }

        if (_descriptionText != null)
        {
            _descriptionText.text = description;
        }

        Open();
    }

    private void OnRewardStoneCollected(RewardStoneCollectedEvent collectedEvent)
    {
        Show(collectedEvent.Type, collectedEvent.Icon, collectedEvent.Description);
    }

    private string GetTypeLabel(RewardType type)
    {
        switch (type)
        {
            case RewardType.ActiveRune:
                return _activeRuneLabel;
            case RewardType.PassiveRune:
                return _passiveRuneLabel;
            default:
                return _abilityLabel;
        }
    }

    private Color GetTypeColor(RewardType type)
    {
        switch (type)
        {
            case RewardType.ActiveRune:
                return _activeRuneColor;
            case RewardType.PassiveRune:
                return _passiveRuneColor;
            default:
                return _abilityColor;
        }
    }
}

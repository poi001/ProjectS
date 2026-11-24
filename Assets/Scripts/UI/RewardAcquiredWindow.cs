using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 리워드 결석을 주워 기본 동작을 얻었을 때 뜨는 획득 창 (임시 그레이박스, GDD 6.6). BattleUI 프리팹에 중첩해 둔다.
//  - AbilityRewardAcquiredEvent를 받으면 패널을 채우고 연다. 패널은 룬 선택 창의 패널 하나와 같은 모양이다. (아이콘 → 종류 팻말 → 설명창)
//  - 액티브 룬 결석은 창 없이 바로 얻고 패시브 룬 결석은 룬 선택 창을 열므로, 지금 이 창을 여는 것은 리워드 결석뿐이다.
//    (액티브 룬·패시브 룬 팻말 설정은 나중에 다른 보상에도 이 창을 쓸 때를 위해 남겨 두었다)
//  - 닫기는 Content 아래의 UIClosePrompt가 맡는다. (Z키 또는 화면 클릭, 깜빡이는 안내 글)
//  - 열린 동안 게임을 일시정지한다. (UIWindow의 Pauses Game)
// 동작 해금은 BattleResultManager가 이 창을 열기 전에 끝낸다. 이 창은 얻은 것을 보여 주기만 한다.
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
        EventBus<AbilityRewardAcquiredEvent>.Subscribe(OnAbilityRewardAcquired);
    }

    protected override void OnDisable()
    {
        EventBus<AbilityRewardAcquiredEvent>.Unsubscribe(OnAbilityRewardAcquired);
        base.OnDisable();
    }

    // 얻은 보상으로 패널을 채우고 창을 연다. 이미 열려 있으면 무시한다.
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

    private void OnAbilityRewardAcquired(AbilityRewardAcquiredEvent acquiredEvent)
    {
        Show(RewardType.Ability, acquiredEvent.Icon, acquiredEvent.Description);
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

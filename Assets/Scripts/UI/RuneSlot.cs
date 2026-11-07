using UnityEngine;
using UnityEngine.UI;

// 룬 슬롯 하나의 표시. 장착된 룬 아이콘과 (액티브 룬의) 재사용 대기시간을 보여 준다.
// 재사용 대기 중에는 아직 차지 않은 아래쪽을 어둡게 덮고, 시간이 지날수록 위에서 아래로 색이 차오른다.
// 룬 시스템 연동은 추후. 외부에서 SetRune / Clear / SetCooldown을 호출한다.
// 스프라이트 없이도 동작하도록, 덮개 Image가 스프라이트가 있는 Filled 타입이면 fillAmount를(Vertical, Origin Bottom으로 설정),
// 그 외에는 RectTransform의 anchorMax.y로 덮는 높이를 표현한다. (anchorMin.y = 0, 아래에서부터 덮음)
public class RuneSlot : MonoBehaviour
{
    [Header("참조")]
    [Tooltip("장착된 룬의 아이콘 이미지입니다. 빈 슬롯이면 숨깁니다.")]
    [SerializeField] private Image _iconImage;

    [Tooltip("재사용 대기 중 아직 차지 않은 부분을 덮는 어두운 이미지입니다. 아이콘 위에 그려지도록 아이콘보다 아래쪽 형제로 둡니다.")]
    [SerializeField] private Image _cooldownImage;

    public bool IsEmpty { get; private set; } = true;

    private void Awake()
    {
        Clear();
    }

    // 룬을 장착한 상태로 표시한다. 아이콘 스프라이트가 아직 없으면 null을 넘겨도 된다. (프리팹의 아이콘 색으로 표시)
    public void SetRune(Sprite icon)
    {
        IsEmpty = false;
        _iconImage.sprite = icon;
        _iconImage.enabled = true;
    }

    // 빈 슬롯으로 표시한다.
    public void Clear()
    {
        IsEmpty = true;
        _iconImage.sprite = null;
        _iconImage.enabled = false;
        SetCooldown(0f, 0f);
    }

    // 재사용 대기시간을 표시한다. 남은 시간이 0 이하이거나 duration이 0 이하면 사용 가능 상태(덮개 없음)로 표시한다.
    // 매 프레임 호출해도 GC 할당이 없다.
    public void SetCooldown(float remainingTime, float duration)
    {
        float remainingRatio = duration > 0f ? Mathf.Clamp01(remainingTime / duration) : 0f;
        _cooldownImage.enabled = remainingRatio > 0f;

        if (_cooldownImage.type == Image.Type.Filled && _cooldownImage.sprite != null)
        {
            _cooldownImage.fillAmount = remainingRatio;
            return;
        }

        RectTransform rectTransform = _cooldownImage.rectTransform;
        Vector2 anchorMax = rectTransform.anchorMax;
        anchorMax.y = remainingRatio;
        rectTransform.anchorMax = anchorMax;
    }
}

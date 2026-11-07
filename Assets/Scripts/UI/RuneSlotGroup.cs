using UnityEngine;
using UnityEngine.UI;

// 같은 종류(액티브/패시브) 룬 슬롯들의 줄. 장착 한도가 늘어나면 보이는 슬롯 수를 늘린다.
// 최대 한도만큼의 슬롯을 미리 Content 아래에 배치해 두고, 활성/비활성으로 보이는 개수만 바꾼다. (실행 중 생성하지 않음)
// 이 오브젝트는 체력바 옆의 남는 공간 전체를 차지하고(폭은 부모 레이아웃이 정한다),
// Content(HorizontalLayoutGroup + ContentSizeFitter)가 그 한가운데에 슬롯들을 모아 배치한다.
// 슬롯이 많아져 남는 공간보다 넓어지면 Content 전체를 같은 비율로 줄여서 공간 안에 맞춘다. (공간이 넉넉하면 원래 크기 유지)
public class RuneSlotGroup : MonoBehaviour
{
    [Header("슬롯")]
    [Tooltip("이 줄의 슬롯들입니다. 0번이 체력바에 가장 가까운 슬롯이며, 개수가 최대 장착 한도입니다.")]
    [SerializeField] private RuneSlot[] _slots;

    [Tooltip("시작할 때 보이는 슬롯 수(시작 장착 한도)입니다. 최대 한도(슬롯 배열 길이)를 넘으면 최대 한도로 맞춥니다.")]
    [SerializeField, Min(0)] private int _initialCapacity = 1;

    [Header("배치")]
    [Tooltip("슬롯들을 담는 자식입니다. 가운데 기준점, HorizontalLayoutGroup, ContentSizeFitter가 있어야 하며, 공간이 모자라면 이 오브젝트의 크기를 줄입니다.")]
    [SerializeField] private RectTransform _content;

    [Tooltip("남는 공간의 양 끝에 비워 두는 여백입니다. 슬롯이 화면 끝이나 체력바에 바짝 붙지 않게 합니다. (단위: 캔버스 기준 픽셀, 양쪽 각각)")]
    [SerializeField, Min(0f)] private float _edgePadding = 24f;

    private float _contentWidth;

    public int Capacity { get; private set; }
    public int MaxCapacity => _slots.Length;

    private void Awake()
    {
        SetCapacity(_initialCapacity);
    }

    // 해상도가 바뀌거나 부모 레이아웃이 폭을 정했을 때 호출된다. 폭이 바뀔 때만 불리므로 매 프레임 비용은 없다.
    private void OnRectTransformDimensionsChange()
    {
        FitContent();
    }

    // 보이는 슬롯 수를 바꾼다. 숨겨지는 슬롯은 비운다.
    public void SetCapacity(int capacity)
    {
        Capacity = Mathf.Clamp(capacity, 0, _slots.Length);

        for (int i = 0; i < _slots.Length; i++)
        {
            bool isVisible = i < Capacity;

            if (!isVisible && !_slots[i].IsEmpty)
            {
                _slots[i].Clear();
            }

            _slots[i].gameObject.SetActive(isVisible);
        }

        // 슬롯 수가 바뀌었으니 Content 폭을 즉시 다시 계산한 뒤 공간에 맞춘다.
        if (_content != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _contentWidth = _content.rect.width;
            FitContent();
        }
    }

    // index번 슬롯을 돌려준다. 보이는 범위(Capacity) 밖이면 null.
    public RuneSlot GetSlot(int index)
    {
        if (index < 0 || index >= Capacity)
        {
            return null;
        }

        return _slots[index];
    }

    // Content가 남는 공간(여백 제외)보다 넓으면 같은 비율로 줄이고, 아니면 원래 크기로 둔다.
    // 아직 폭이 정해지지 않은 첫 프레임에는 줄이지 않고, 폭이 정해진 뒤 다시 호출되면서 맞춰진다.
    private void FitContent()
    {
        if (_content == null || _contentWidth <= 0f)
        {
            return;
        }

        float regionWidth = ((RectTransform)transform).rect.width;
        float scale = 1f;

        if (regionWidth > 0f)
        {
            float availableWidth = Mathf.Max(regionWidth - _edgePadding * 2f, 0f);
            scale = Mathf.Clamp01(availableWidth / _contentWidth);
        }

        _content.localScale = new Vector3(scale, scale, 1f);
    }
}

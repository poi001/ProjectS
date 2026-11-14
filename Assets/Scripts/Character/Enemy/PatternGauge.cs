using System;
using UnityEngine;

// 보스 패턴 게이지. 현재 값과 최대값만 보관하는 범용 게이지다.
// 언제 얼마나 채우고 줄일지, 꽉 찼을 때 무엇을 할지는 보스마다 다르므로 각 보스(와 그 상태)가 정한다.
// (예: Knight는 공격할수록 차고 플레이어를 맞히면 줄며, 꽉 찬 상태에서 플레이어에게 피해를 입으면 기절한다.)
// 게이지를 쓰는 보스만 프리팹 루트에 이 컴포넌트를 붙인다. 없으면 Enemy.PatternGauge가 null이고, 보스 체력바 아래의 게이지 UI도 숨겨진다.
public class PatternGauge : MonoBehaviour
{
    [Tooltip("게이지의 최대값입니다. 보스가 채우거나 줄이는 양도 이 값을 기준으로 정합니다.\n100이면 채우는 양을 퍼센트처럼 쓸 수 있습니다.")]
    [SerializeField, Min(1f)] private float _maxValue = 100f;

    public float MaxValue => _maxValue;
    public float CurrentValue { get; private set; }
    // 0이면 비어 있고 1이면 꽉 찬 상태
    public float Ratio => CurrentValue / _maxValue;
    public bool IsFull => CurrentValue >= _maxValue;
    // 꽉 찼을 때 게이지 UI가 강조(튕김·깜빡임)를 보여 줄지. 꽉 찬 것이 "지금 때리면 기절"처럼 플레이어에게 알릴 일일 때만 켠다.
    // 전투가 시작되면 켜져 있고, 보스가 필요할 때 SetFullEmphasisEnabled로 끈다. (예: SoulKnight 2페이즈)
    public bool ShowsFullEmphasis { get; private set; } = true;

    // 초기화·채움·줄임으로 값이 바뀌거나 강조 표시 여부가 바뀔 때마다 호출된다. (게이지 UI용) 바뀐 값은 CurrentValue/Ratio로 읽는다.
    public event Action Changed;

    // 전투 시작 시(Enemy.OnInit) 호출한다. 게이지를 비우고 강조 표시를 다시 켠다.
    public void Initialize()
    {
        CurrentValue = 0f;
        ShowsFullEmphasis = true;
        Changed?.Invoke();
    }

    // 꽉 찼을 때의 UI 강조를 켜거나 끈다. 값이 그대로면 Changed를 호출하지 않는다.
    public void SetFullEmphasisEnabled(bool isEnabled)
    {
        if (ShowsFullEmphasis == isEnabled)
        {
            return;
        }

        ShowsFullEmphasis = isEnabled;
        Changed?.Invoke();
    }

    public void Add(float amount)
    {
        SetValue(CurrentValue + amount);
    }

    public void Reduce(float amount)
    {
        SetValue(CurrentValue - amount);
    }

    // 0 ~ 최대값 사이로 잘라서 적용한다. 값이 그대로면 Changed를 호출하지 않는다.
    public void SetValue(float value)
    {
        float clampedValue = Mathf.Clamp(value, 0f, _maxValue);

        if (clampedValue == CurrentValue)
        {
            return;
        }

        CurrentValue = clampedValue;
        Changed?.Invoke();
    }
}

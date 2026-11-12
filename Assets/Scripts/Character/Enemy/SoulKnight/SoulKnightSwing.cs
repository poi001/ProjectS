using System;
using UnityEngine;

// SoulKnight 휘두르기 공격(Attack1·Attack2·Smash) 한 번의 설정. 판정 범위·피해 배율과 휘두르면서 전진하는 구간을 정한다.
// 판정 시점은 애니메이션 이벤트(OnAttackHitFrame)가 정한다. 이벤트가 올 때마다 이 범위로 한 번 판정한다.
[Serializable]
public class SoulKnightSwing
{
    [Tooltip("이 공격의 판정 범위입니다. 칼이 휘둘러지는 영역에 맞춥니다. SoulKnight 위치 기준이며 오프셋 x는 바라보는 방향 기준입니다.\n판정은 클립의 애니메이션 이벤트(OnAttackHitFrame) 시점에 한 번 나갑니다. SoulKnight를 선택하면 Scene 뷰에 범위가 보입니다.")]
    [SerializeField] private HitboxShape _hitbox;
    [Tooltip("켜면 SoulKnight를 선택했을 때 Scene 뷰(프리팹 편집 화면 포함)에 이 공격의 판정 범위를 붉은 박스로 보여 줍니다. 끄면 이 공격의 박스만 숨깁니다.\n에디터 표시만 바꾸며 게임 동작에는 영향이 없습니다.")]
    [SerializeField] private bool _showHitbox = true;
    [Tooltip("이 공격의 피해량 배율입니다. 실제 피해 = SoulKnight 공격력 × 이 값입니다. 1 = 공격력 그대로")]
    [SerializeField, Min(0f)] private float _damageMultiplier = 1f;
    [Tooltip("휘두르면서 전진을 시작하는 시점입니다. 공격 애니메이션 진행도(0~1) 기준입니다. 전진 속도는 '공격 (공통)'의 전진 속도를 씁니다.\n시작과 끝이 같으면 전진하지 않습니다. Smash는 이 값과 관계없이 전진하지 않습니다.")]
    [SerializeField, Range(0f, 1f)] private float _stepStartProgress;
    [Tooltip("전진을 끝내는 시점입니다. 공격 애니메이션 진행도(0~1) 기준입니다.")]
    [SerializeField, Range(0f, 1f)] private float _stepEndProgress;

    public HitboxShape Hitbox => _hitbox;
    public bool ShowHitbox => _showHitbox;
    public float DamageMultiplier => _damageMultiplier;
    public float StepStartProgress => _stepStartProgress;
    public float StepEndProgress => _stepEndProgress;

    public SoulKnightSwing(HitboxShape hitbox, float damageMultiplier, float stepStartProgress, float stepEndProgress)
    {
        _hitbox = hitbox;
        _damageMultiplier = damageMultiplier;
        _stepStartProgress = stepStartProgress;
        _stepEndProgress = stepEndProgress;
    }
}

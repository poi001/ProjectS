using System;
using UnityEngine;

// Knight 공격 1회/2회의 한 번의 휘두름 설정. 애니메이션의 어느 구간에, 어떤 범위로, 얼마의 피해로 판정을 내는지와 그때 얼마나 전진하는지를 정한다.
// 칼을 휘두르기 시작하는 시점(HitStartTime)에 판정과 전진이 함께 시작된다.
// 구간 안에서는 매 물리 프레임 판정을 내지만, 같은 대상은 한 번의 휘두름에서 한 번만 맞는다. (AttackHitChecker가 보장)
[Serializable]
public class KnightSwing
{
    [Tooltip("칼을 휘두르기 시작하는 시점입니다. 이 시점부터 공격 판정과 전진이 함께 시작됩니다. 공격 애니메이션 진행도(0~1) 기준이며, 준비 자세 정지가 끝나고 휘두르기 시작하는 프레임에 맞춥니다.\n공격 클립의 프레임 시간을 바꾸면 함께 맞춰야 합니다.")]
    [SerializeField, Range(0f, 1f)] private float _hitStartTime = 0.545f;
    [Tooltip("판정이 끝나는 시점입니다. 공격 애니메이션 진행도(0~1) 기준이며, 칼이 다 휘둘러지는 프레임에 맞춥니다.\n시작~끝 사이에만 판정이 나가며, 이 구간 안에서도 같은 대상은 한 번만 맞습니다.")]
    [SerializeField, Range(0f, 1f)] private float _hitEndTime = 0.848f;
    [Tooltip("이 휘두름의 피해량 배율입니다. 실제 피해 = Knight 공격력 × 이 값입니다.\n1 = 공격력 그대로")]
    [SerializeField, Min(0f)] private float _damageMultiplier = 1f;
    [Tooltip("휘두르면서 전진하는 시간 길이입니다. 공격 애니메이션 진행도(0~1) 기준이며, 0.25 = 애니메이션 전체의 25% 동안 전진합니다.\n전진 속도는 Knight의 '공격 (공통)' 전진 속도를 씁니다.")]
    [SerializeField, Range(0f, 1f)] private float _stepLength = 0.25f;
    [Tooltip("이 휘두름의 공격 판정 범위입니다. 칼이 실제로 휘둘러지는 영역에 맞춥니다. Knight를 선택하면 Scene 뷰에 범위가 붉은 선으로 보입니다.")]
    [SerializeField] private HitboxShape _hitbox;

    public float HitStartTime => _hitStartTime;
    public float HitEndTime => _hitEndTime;
    public float DamageMultiplier => _damageMultiplier;
    public float StepLength => _stepLength;
    public HitboxShape Hitbox => _hitbox;

    public KnightSwing(float hitStartTime, float hitEndTime, float damageMultiplier, float stepLength, HitboxShape hitbox)
    {
        _hitStartTime = hitStartTime;
        _hitEndTime = hitEndTime;
        _damageMultiplier = damageMultiplier;
        _stepLength = stepLength;
        _hitbox = hitbox;
    }
}

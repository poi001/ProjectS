using System;
using UnityEngine;

// StoneGolem의 내려치기(공격, 스킬) 한 번의 설정. 애니메이션의 어느 구간에, 어떤 범위로, 얼마의 피해로 판정을 내는지 정한다.
// 구간 안에서는 매 물리 프레임 판정을 내지만, 같은 대상은 한 번의 내려치기에서 한 번만 맞는다. (AttackHitChecker가 보장)
[Serializable]
public class StoneGolemStrike
{
    [Tooltip("공격 판정이 시작되는 시점입니다. 애니메이션 진행도(0~1) 기준이며, 주먹이 바닥에 닿기 시작하는 그림에 맞춥니다.\n클립의 프레임 시간을 바꾸면 함께 맞춰야 합니다.")]
    [SerializeField, Range(0f, 1f)] private float _hitStartTime;
    [Tooltip("공격 판정이 끝나는 시점입니다. 애니메이션 진행도(0~1) 기준입니다.\n시작~끝 사이에만 판정이 나가며, 이 구간 안에서도 같은 대상은 한 번만 맞습니다.")]
    [SerializeField, Range(0f, 1f)] private float _hitEndTime;
    [Tooltip("피해량 배율입니다. 실제 피해 = 스탯의 공격력 x 이 값입니다. 기획이 정해지기 전의 임시값입니다.\n1 = 공격력 그대로")]
    [SerializeField, Min(0f)] private float _damageMultiplier = 1f;
    [Tooltip("공격 판정 범위입니다. 주먹과 궤적이 실제로 닿는 영역에 맞춥니다. StoneGolem을 선택하면 Scene 뷰에 범위가 붉은 선으로 보입니다.")]
    [SerializeField] private HitboxShape _hitbox;

    public float HitStartTime => _hitStartTime;
    public float HitEndTime => _hitEndTime;
    public float DamageMultiplier => _damageMultiplier;
    public HitboxShape Hitbox => _hitbox;

    public StoneGolemStrike(float hitStartTime, float hitEndTime, float damageMultiplier, HitboxShape hitbox)
    {
        _hitStartTime = hitStartTime;
        _hitEndTime = hitEndTime;
        _damageMultiplier = damageMultiplier;
        _hitbox = hitbox;
    }
}

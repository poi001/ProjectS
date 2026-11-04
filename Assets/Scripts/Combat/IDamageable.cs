// 데미지를 받을 수 있는 대상(플레이어, 보스) 공통 인터페이스.
public interface IDamageable
{
    // 피격이 실제로 적용되었으면 true를 반환한다. (이미 사망한 경우 등은 false)
    // 추후 실드 등이 생기면 실제로 적용된 데미지 양을 반환하도록 바꿀 수 있다.
    bool TakeDamage(DamageInfo damageInfo);
}

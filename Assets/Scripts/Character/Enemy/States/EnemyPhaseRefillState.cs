using UnityEngine;

// 페이즈 전환의 체력 충전 상태. (모든 보스 공통)
// 제자리에서 체력을 0에서 다음 페이즈의 최대 체력까지 정해진 시간 동안 채우고, 다 차면 다음 페이즈를 시작한다.
// 애니메이션은 바꾸지 않는다. 보스별 전환 연출이 틀어 둔 모습이 그대로 이어진다.
// 진입은 Enemy.StartPhaseRefill로만 한다. 충전 중에는 피해를 입지 않는다. (CharacterVital의 소진 상태)
public class EnemyPhaseRefillState : EnemyState
{
    private float _enterTime;

    public EnemyPhaseRefillState(Enemy owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _enterTime = Time.time;
        owner.Movement.SetVelocityX(0f);
        owner.Vital.SetRefillHealth(owner.NextPhaseMaxHealth, 0f);
    }

    public override void LogicUpdate()
    {
        float duration = owner.PhaseRefillDuration;
        float ratio = duration > 0f ? Mathf.Clamp01((Time.time - _enterTime) / duration) : 1f;
        owner.Vital.SetRefillHealth(owner.NextPhaseMaxHealth, ratio);

        if (ratio >= 1f)
        {
            owner.CompletePhaseTransition();
        }
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.SetVelocityX(0f);
    }
}

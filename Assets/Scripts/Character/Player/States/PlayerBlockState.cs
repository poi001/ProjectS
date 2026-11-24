using System;

// 블로킹 상태. 땅 위에서 블로킹 키(아래 방향키)를 누르면 막는 동작(Block)을 한 번 재생한다.
// 재생되는 동안 들어온 공격을 한 번 막는다. (피해 없음) 한 번 막은 뒤에는 남은 동작 동안 다시 막지 못한다.
// 막는 데 성공하면 짧은 슬로우 모션과 소리를 재생한다. (Player.PlayBlockSuccessEffects)
// 동작이 끝날 때까지 이동·점프·공격·대시 입력을 받지 않고 멈춰 있으며, 방향도 바뀌지 않는다.
// 시작할 때 기력을 소모한다. (막았는지와 관계없음, Player.ConsumeBlockStamina)
// 끝나면 쿨타임이 시작되고(막았는지와 관계없음) Idle/Run(입력이 있으면 그 동작)으로 돌아간다.
// 막는 방법: 재생 중 CharacterVital의 피해 무효를 켜 두고, 무효 처리된 첫 공격에서 끈다.
// (피해 무효는 맞은 것으로 처리되어 히트 이펙트와 0 데미지 숫자가 뜬다)
public class PlayerBlockState : PlayerState
{
    // 상태에 진입할 때마다 델리게이트가 새로 할당되지 않도록 한 번만 만들어 둔다.
    private readonly Action<DamageInfo> _damageNullifiedHandler;

    public PlayerBlockState(Player owner, StateMachine<Player> stateMachine)
        : base(owner, stateMachine)
    {
        _damageNullifiedHandler = OnDamageNullified;
    }

    public override void Enter()
    {
        owner.InputHandler.ConsumeBlockBuffer();
        owner.ConsumeBlockStamina();
        owner.Vital.DamageNullified += _damageNullifiedHandler;
        owner.Vital.SetDamageNullified(true);
        owner.Movement.Move(0f);
        owner.Animation.Play(PlayerAnimation.Block);
    }

    // 피격·사망 등으로 중간에 끊겨도 피해 무효가 켜진 채로 남지 않게 여기서 되돌린다.
    public override void Exit()
    {
        owner.Vital.DamageNullified -= _damageNullifiedHandler;
        owner.Vital.SetDamageNullified(false);
        owner.StartBlockCooldown();
    }

    public override void LogicUpdate()
    {
        if (!owner.Animation.IsFinished(PlayerAnimation.Block))
        {
            return;
        }

        // 블로킹 중 발밑이 사라졌더라도 막는 동작을 끝까지 마친 뒤에 낙하한다.
        if (owner.Movement.IsGrounded)
        {
            ChangeToGroundedState(owner.Animation.GetTimeSinceFinished(PlayerAnimation.Block));
        }
        else
        {
            stateMachine.ChangeState(owner.FallState);
        }
    }

    public override void PhysicsUpdate()
    {
        owner.Movement.Move(0f);
    }

    // 공격을 한 번 막았다. 이번 블로킹으로는 더 막지 못하게 피해 무효를 끈다.
    private void OnDamageNullified(DamageInfo damageInfo)
    {
        owner.Vital.SetDamageNullified(false);
        owner.PlayBlockSuccessEffects();
    }
}

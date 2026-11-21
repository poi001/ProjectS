using UnityEngine;

// 대시 마무리. 돌진이 끝난 자리에서 마무리 모션을 재생한다. 들어오는 순간 이어서 대시할지 고른다. (SkullDog.RollDashChain)
//  - 이어서 대시: 그 자리(공중이면 공중)에 중력 없이 떠 있다가, 마무리 모션이 끝나면 대시 준비(DashReady)로 간다.
//  - 이어서 대시하지 않음: 중력을 받아 곧장 떨어진다. 마무리 모션이 끝나고 바닥에 닿아 있으면 Idle로 쉬어간다.
//    떨어지는 중이면 마지막 그림을 유지한 채 착지를 기다린다.
// 이 상태에서는 몸에 닿아도 피해가 없다. (피해는 돌진하는 동안에만)
// 애니메이션: DashEnd(Sprite Sheet_9 → 10, 반복 없이 마지막 그림 유지)
public class SkullDogDashEndState : SkullDogState
{
    // 이번 마무리 뒤에 이어서 대시하는지. 들어올 때 정한다.
    private bool _isChaining;

    public SkullDogDashEndState(SkullDog owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _isChaining = !skullDog.IsTargetDead && skullDog.RollDashChain();

        if (_isChaining)
        {
            skullDog.Movement.SetGravityEnabled(false);
            skullDog.Movement.SetVelocity(Vector2.zero);
        }
        else
        {
            skullDog.Movement.SetVelocityX(0f);
        }

        skullDog.Animation.Play(SkullDogAnimation.DashEnd);
    }

    // 이어서 대시하려고 떠 있던 중에 끊기는 경우(플레이어 사망, SkullDog 사망 등)에도 중력을 되돌려 떨어지게 한다.
    public override void Exit()
    {
        skullDog.Movement.SetGravityEnabled(true);
    }

    public override void LogicUpdate()
    {
        if (!skullDog.Animation.IsFinished(SkullDogAnimation.DashEnd))
        {
            return;
        }

        if (_isChaining)
        {
            stateMachine.ChangeState(skullDog.DashReadyState);
            return;
        }

        if (skullDog.Movement.IsGrounded)
        {
            skullDog.ChangeToRest();
        }
    }

    public override void PhysicsUpdate()
    {
        if (_isChaining)
        {
            skullDog.Movement.SetVelocity(Vector2.zero);
            return;
        }

        skullDog.Movement.SetVelocityX(0f);
    }
}

using UnityEngine;

// 샷. 플라이로 날다가 고른 쪽(왼쪽/오른쪽) 왕복 끝에 닿았을 때 들어온다. 끝날 때까지 그 자리에 가만히 떠 있는다.
// 순서: 준비자세(ShotStart 애니메이션) → 발사(Shot 애니메이션, 마지막 그림에서 멈춤) → 끝 모션(ShotEnd 애니메이션) → 플라이
//  - 발사: 플레이어 쪽으로 부채꼴 발사(Mossback.FireVolley)를 ShotVolleyCount번 한다. 첫 발사는 준비자세가 끝나자마자,
//    그다음부터는 ShotVolleyInterval 간격으로 한다. 발사할 때마다 플레이어 쪽으로 돌아서서 다시 조준한다.
//  - 마지막 발사 뒤에도 ShotVolleyInterval만큼 기다린 다음 끝 모션으로 넘어간다.
// 몸에 닿아도 피해가 없다. 피해는 발사체(MossbackProjectile)만 준다.
// 애니메이션: 스매시와 같은 그림을 원래 속도로 쓴다. ShotStart(Enemy3-AttackSmashStart), Shot(Enemy3-AttackSmashLoop, 반복 없음), ShotEnd(Enemy3-SmashEnd)
public class MossbackShotState : MossbackState
{
    private enum Step
    {
        Ready,
        Firing,
        End,
    }

    private Step _step;
    // 지금까지 한 부채꼴 발사 횟수
    private int _firedVolleyCount;
    // 다음 발사(또는 끝 모션)로 넘어가는 시각
    private float _nextStepTime;

    public MossbackShotState(Mossback owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _step = Step.Ready;
        _firedVolleyCount = 0;

        mossback.Movement.SetVelocity(Vector2.zero);
        FaceTargetIfAlive();
        mossback.Animation.Play(MossbackAnimation.ShotStart);
    }

    public override void LogicUpdate()
    {
        switch (_step)
        {
            case Step.Ready:
                if (mossback.Animation.IsFinished(MossbackAnimation.ShotStart))
                {
                    _step = Step.Firing;
                    _nextStepTime = Time.time;
                    mossback.Animation.Play(MossbackAnimation.Shot);
                }

                break;

            case Step.Firing:
                UpdateFiring();
                break;

            case Step.End:
                if (mossback.Animation.IsFinished(MossbackAnimation.ShotEnd))
                {
                    stateMachine.ChangeState(mossback.FlyState);
                }

                break;
        }
    }

    public override void PhysicsUpdate()
    {
        mossback.Movement.SetVelocity(Vector2.zero);
    }

    private void UpdateFiring()
    {
        if (Time.time < _nextStepTime)
        {
            return;
        }

        if (_firedVolleyCount >= mossback.ShotVolleyCount)
        {
            _step = Step.End;
            mossback.Animation.Play(MossbackAnimation.ShotEnd);
            return;
        }

        FaceTargetIfAlive();
        mossback.FireVolley();
        _firedVolleyCount++;
        _nextStepTime = Time.time + mossback.ShotVolleyInterval;
    }

    private void FaceTargetIfAlive()
    {
        if (!mossback.IsTargetDead)
        {
            mossback.FaceTarget();
        }
    }
}

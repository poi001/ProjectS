// Idle과 실제 사망은 Enemy 공통 상태를 사용한다. Death 미리보기는 사망 이벤트 없이 모션만 재생한다.
public sealed class SoulKnightMotionState : EnemyState
{
    private readonly SoulKnight _boss;
    private readonly SoulKnightMotion _motion;
    private readonly int _hash;

    public SoulKnightMotionState(SoulKnight boss, StateMachine<Enemy> machine, SoulKnightMotion motion)
        : base(boss, machine)
    {
        _boss = boss;
        _motion = motion;
        _hash = SoulKnightAnimation.GetMotionHash(motion);
    }

    public override void Enter()
    {
        _boss.Movement.SetVelocityX(0f);
        _boss.Animation.Play(_hash);
        if (_motion == SoulKnightMotion.Roll)
        {
            _boss.SetHurtboxEnabled(false);
        }
    }

    public override void Exit()
    {
        if (_motion == SoulKnightMotion.Roll)
        {
            _boss.SetHurtboxEnabled(true);
        }
    }

    public override void LogicUpdate()
    {
        if (_motion == SoulKnightMotion.CrouchIdle && !_boss.AutoPreview)
        {
            return;
        }

        if (!_boss.Animation.IsFinished(_hash))
        {
            return;
        }

        switch (_motion)
        {
            case SoulKnightMotion.Attack1:
                _boss.PlayMotion(SoulKnightMotion.Attack2);
                break;
            case SoulKnightMotion.Attack2:
                _boss.PlayMotion(SoulKnightMotion.Attack3);
                break;
            case SoulKnightMotion.Attack3:
                _boss.PlayMotion(SoulKnightMotion.Attack4);
                break;
            case SoulKnightMotion.Jump:
                _boss.PlayMotion(SoulKnightMotion.JumpAttack);
                break;
            default:
                _boss.ChangeToIdle(1f);
                break;
        }
    }
}

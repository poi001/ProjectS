using UnityEngine;

// 스킬(메테오). 패턴 게이지가 꽉 찬 채로 Idle에 들어가면 들어온다. (BloodWing.TryInterruptIdle)
// 세 단계로 진행한다.
//  1. 가운데로 이동: 지금 자리에서 화면 가로 가운데(대기 위치 높이)까지 SkillMoveDuration 동안 일정하게 이동한다.
//  2. 메테오: 게이지가 GaugeDrainDuration에 걸쳐 0%까지 줄어드는 동안, MeteorInterval마다 두 벽 사이 무작위 위치에 운석이 떨어진다.
//     AimedMeteorEvery개마다 하나는 그 순간의 플레이어 머리 위로 떨어진다. (벽에 붙어 있어도 맞을 수 있게)
//     그중 무작위로 하나는 바닥에 남는 운석이다. 게이지가 0%가 되면 운석을 멈춘다. (이미 떨어지던 운석은 끝까지 떨어진다)
//  3. 원래 자리로 이동: 스킬에 들어오기 전 자리까지 SkillMoveDuration 동안 일정하게 이동한 뒤 화면 안쪽을 보고 Idle로 쉰다.
// 이동하는 동안에는 게이지가 줄지 않고 운석도 떨어지지 않는다. 몸에 닿아도 피해는 없다.
// 애니메이션: Skill(ATTACK 시트를 0.5초 주기로 반복)
public class BloodWingSkillState : BloodWingState
{
    private enum Phase
    {
        MoveIn,
        Meteor,
        MoveOut,
    }

    private readonly BloodWingLinearMove _move = new BloodWingLinearMove();
    private Phase _phase;
    // 스킬에 들어오기 전 자리. 끝나면 여기로 돌아간다.
    private Vector2 _returnPoint;
    private float _nextMeteorTime;
    // 이 시각 이후 처음 떨어지는 운석이 바닥에 남는 운석이다.
    private float _remainingMeteorTime;
    private bool _hasDroppedRemainingMeteor;
    // 이번 스킬에서 떨어뜨린 운석 수. Aimed Meteor Every개마다 하나를 플레이어 머리 위로 떨어뜨리는 데 쓴다.
    private int _droppedMeteorCount;

    public BloodWingSkillState(BloodWing owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _returnPoint = bloodWing.Movement.Position;
        Vector2 skillPoint = bloodWing.GetSkillPoint();

        _phase = Phase.MoveIn;
        _move.Begin(_returnPoint, skillPoint, bloodWing.SkillMoveDuration);
        bloodWing.Movement.SetVelocity(Vector2.zero);
        bloodWing.Animation.UpdateFacing(skillPoint.x - _returnPoint.x);
        bloodWing.Animation.Play(BloodWingAnimation.Skill);
    }

    // 스킬이 중간에 끊기는 경우(플레이어 사망, BloodWing 사망 등)에도 그 자리에 멈춘다. 이미 떨어지던 운석은 끝까지 떨어진다.
    public override void Exit()
    {
        bloodWing.Movement.SetVelocity(Vector2.zero);
    }

    public override void LogicUpdate()
    {
        if (_phase != Phase.Meteor)
        {
            return;
        }

        if (Time.time >= _nextMeteorTime)
        {
            _nextMeteorTime += bloodWing.MeteorInterval;
            bool remainsOnGround = !_hasDroppedRemainingMeteor && Time.time >= _remainingMeteorTime;
            _hasDroppedRemainingMeteor |= remainsOnGround;
            _droppedMeteorCount++;
            int aimedEvery = bloodWing.AimedMeteorEvery;
            bool aimAtTarget = aimedEvery > 0 && _droppedMeteorCount % aimedEvery == 0;
            bloodWing.DropMeteor(remainsOnGround, aimAtTarget);
        }

        if (bloodWing.DrainGauge(Time.deltaTime))
        {
            _phase = Phase.MoveOut;
            _move.Begin(bloodWing.Movement.Position, _returnPoint, bloodWing.SkillMoveDuration);
            bloodWing.Animation.UpdateFacing(_returnPoint.x - bloodWing.Movement.Position.x);
        }
    }

    public override void PhysicsUpdate()
    {
        if (_phase == Phase.Meteor)
        {
            bloodWing.Movement.SetVelocity(Vector2.zero);
            return;
        }

        if (!_move.Step(bloodWing.Movement))
        {
            return;
        }

        if (_phase == Phase.MoveIn)
        {
            StartMeteor();
            return;
        }

        bloodWing.FaceFieldCenter();
        bloodWing.ChangeToIdle(bloodWing.PatternInterval);
    }

    // 가운데에 도착: 플레이어 쪽을 보고 운석을 떨어뜨리기 시작한다.
    // 바닥에 남는 운석은 게이지가 다 줄기 전(마지막 간격보다 여유 있게 앞)의 무작위 시각에 정해, 스킬 한 번에 반드시 하나 떨어지게 한다.
    private void StartMeteor()
    {
        _phase = Phase.Meteor;
        _nextMeteorTime = Time.time;
        _remainingMeteorTime = Time.time + Random.Range(0f, Mathf.Max(0f, bloodWing.GaugeDrainDuration - bloodWing.MeteorInterval * 1.5f));
        _hasDroppedRemainingMeteor = false;
        _droppedMeteorCount = 0;

        if (!bloodWing.IsTargetDead)
        {
            bloodWing.FaceTarget();
        }
    }
}

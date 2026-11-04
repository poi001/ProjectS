using UnityEngine;

// 벽타기 후 슬라이딩. 벽에서 플레이어 쪽으로 빠르게 날아가 바닥을 따라 미끄러진다. (GDD 6.6)
// 애니메이션: Slide_Start → Slide_Loop(반복) → Slide_End
// 시작 시점의 플레이어 위치를 일정 거리 지나치거나, 지형(벽, 발판 옆면)에 막히거나, 최대 시간을 넘기면 가로 이동을 멈추고,
// 착지해 있으면 종료 애니메이션을 재생한 뒤 Idle 또는 다음 패턴으로 넘어간다.
// 플레이어에게 닿으면 대미지를 주는 판정은 아직 구현하지 않았다.
public class KnightSlideState : KnightState
{
    private float _direction;
    private float _stopX;
    private float _enterTime;
    private bool _isMoving;
    private bool _isLooping;
    private bool _isEnding;

    public KnightSlideState(Knight owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _direction = knight.DirectionToTarget;
        _stopX = knight.Target.transform.position.x + _direction * knight.SlideOvershoot;
        _enterTime = Time.time;
        _isMoving = true;
        _isLooping = false;
        _isEnding = false;

        knight.FaceTarget();
        knight.Animation.Play(KnightAnimation.SlideStart);
    }

    public override void Exit()
    {
        knight.Movement.SetVelocityX(0f);
    }

    public override void LogicUpdate()
    {
        if (_isEnding)
        {
            if (knight.Animation.IsFinished(KnightAnimation.SlideEnd))
            {
                knight.ChangeToIdleOrNextPattern();
            }

            return;
        }

        if (!_isLooping && knight.Animation.IsFinished(KnightAnimation.SlideStart))
        {
            _isLooping = true;
            knight.Animation.Play(KnightAnimation.SlideLoop);
        }

        if (_isMoving && ShouldStopMoving())
        {
            _isMoving = false;
        }

        if (!_isMoving && knight.Movement.IsGrounded)
        {
            _isEnding = true;
            knight.Animation.Play(KnightAnimation.SlideEnd);
        }
    }

    public override void PhysicsUpdate()
    {
        SetPatternVelocityX(_isMoving ? _direction * knight.SlideSpeed : 0f);
    }

    private bool ShouldStopMoving()
    {
        float positionX = knight.transform.position.x;
        bool hasPassedStopX = _direction > 0f ? positionX >= _stopX : positionX <= _stopX;
        bool isTimeOver = Time.time - _enterTime >= knight.SlideMaxDuration;
        return hasPassedStopX || isTimeOver || knight.Movement.IsBlocked(_direction);
    }
}

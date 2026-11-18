using UnityEngine;

// 점프. 제자리에서 준비 동작을 보여 준 뒤, 마지막 그림을 유지한 채 정해진 높이까지 한 번에 솟아오른다.
//  - 땅에서 시작하면 일반 점프(JumpHeight, 준비 JumpReadyDuration 0.4초 + 상승 JumpRiseDuration 0.1초),
//    공중에서 시작하면 공중점프(AirJumpHeight, 준비 AirJumpReadyDuration 0.2초 + 상승 AirJumpRiseDuration 0.05초)다.
//    공중점프는 공중점프 횟수를 하나 쓴다. (Mushroom.ConsumeAirJump, 땅에 닿으면 다시 채워진다)
//  - 준비: 그 자리(공중점프는 공중의 그 자리)에 멈춰 점프 애니메이션을 재생한다. 중력을 꺼 두므로 공중에서도 떨어지지 않는다.
//  - 상승: 준비가 끝나면 애니메이션의 마지막 그림(Attack_4)을 유지한 채 솟아오른다.
//    속도는 시간에 비례해 줄어들어, 끝나는 순간의 속도가 시작 속도의 JumpEndSpeedRatio배(3분의 1)가 된다.
//    평균 속도가 시작 속도의 (1 + 비율) / 2배이므로 시작 속도 = 2 x 높이 / ((1 + 비율) x 상승 시간)이다.
//    매 물리 스텝의 속도를 그 스텝 가운데 시각의 값으로 정해, 스텝을 모두 더한 높이가 정한 높이와 같게 한다.
//  - 준비·상승 동안 중력을 끄고 속도를 직접 정한다. 가로로는 움직이지 않는다. 이 상태에서는 몸에 닿아도 피해가 없다.
// 정한 높이에 닿으면 바로 점프공격, 또는 공중점프가 남아 있으면 한 번 더 점프한다. (Mushroom.ChangeAfterJump)
// 꼭대기에서 멈춰 기다리는 시간은 점프공격의 준비 동작(MushroomJumpAttackState)이 맡는다.
// 연출: 공중점프가 솟아오르기 시작할 때 발밑에 효과(pixelFX impacts/air-jump)를 띄운다. (Mushroom.SpawnAirJumpDust)
// 애니메이션: Jump(Mushroom-Attack_0 ~ 4, 0.4초, 반복 없이 마지막 그림 유지), 공중점프는 AirJump(같은 클립 2배속, 0.2초)
public class MushroomJumpState : MushroomState
{
    // 남은 시간이 이 값 이하면 그 단계가 끝난 것으로 본다. (단위: 초)
    private const float FinishTimeEpsilon = 0.0001f;

    private float _readyDuration;
    private float _riseDuration;
    // 상승을 시작하는 순간의 올라가는 속도 (단위: 월드 거리/초)
    private float _startSpeed;
    // 준비 동작을 한 시간. 물리 스텝마다 더한다.
    private float _readyElapsedTime;
    // 올라간 시간. 물리 스텝마다 더한다.
    private float _riseElapsedTime;
    // 준비가 끝나고 솟아오르는 중인지 여부
    private bool _isRising;
    // 공중에서 시작한 점프(공중점프)인지 여부
    private bool _isAirJump;

    public MushroomJumpState(Mushroom owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _isAirJump = !mushroom.Movement.IsGrounded;
        float height = _isAirJump ? mushroom.AirJumpHeight : mushroom.JumpHeight;
        _readyDuration = _isAirJump ? mushroom.AirJumpReadyDuration : mushroom.JumpReadyDuration;
        _riseDuration = _isAirJump ? mushroom.AirJumpRiseDuration : mushroom.JumpRiseDuration;
        _startSpeed = 2f * height / ((1f + mushroom.JumpEndSpeedRatio) * _riseDuration);
        _readyElapsedTime = 0f;
        _riseElapsedTime = 0f;
        _isRising = false;

        if (_isAirJump)
        {
            mushroom.ConsumeAirJump();
        }

        if (!mushroom.IsTargetDead)
        {
            mushroom.FaceTarget();
        }

        // 준비 동안 그 자리에 멈춰 있도록 중력을 끈다. (공중점프는 공중에서 떨어지지 않는다)
        mushroom.Movement.SetGravityEnabled(false);
        mushroom.Movement.SetVelocity(Vector2.zero);
        mushroom.Animation.Play(_isAirJump ? MushroomAnimation.AirJump : MushroomAnimation.Jump);
    }

    // 점프가 중간에 끊기는 경우(플레이어 사망, Mushroom 사망 등)에도 그 자리에 멈추고 중력을 되돌린다.
    public override void Exit()
    {
        mushroom.Movement.SetGravityEnabled(true);
        mushroom.Movement.SetVelocity(Vector2.zero);
    }

    public override void PhysicsUpdate()
    {
        if (!_isRising)
        {
            if (_readyDuration - _readyElapsedTime > FinishTimeEpsilon)
            {
                mushroom.Movement.SetVelocity(Vector2.zero);
                _readyElapsedTime += Time.fixedDeltaTime;
                return;
            }

            StartRise();
        }

        float remainingTime = _riseDuration - _riseElapsedTime;

        if (remainingTime <= FinishTimeEpsilon)
        {
            mushroom.ChangeAfterJump();
            return;
        }

        // 마지막 스텝이 남은 시간보다 길면 남은 시간만큼만 올라가도록 속도를 줄인다.
        float stepTime = Mathf.Min(Time.fixedDeltaTime, remainingTime);
        float midProgress = (_riseElapsedTime + stepTime * 0.5f) / _riseDuration;
        float speed = _startSpeed * Mathf.Lerp(1f, mushroom.JumpEndSpeedRatio, midProgress);

        mushroom.Movement.SetVelocity(new Vector2(0f, speed * (stepTime / Time.fixedDeltaTime)));
        _riseElapsedTime += stepTime;
    }

    // 준비를 마치고 솟아오르기 시작한다. 그림은 애니메이션의 마지막 그림(Attack_4)에 멈춰 있다. (반복 없는 클립)
    private void StartRise()
    {
        _isRising = true;

        // 뛰는 순간부터 공중으로 판정되게 한다. (직후 판정 박스가 바닥과 겹쳐 접지로 오판정되는 것을 막는다)
        mushroom.Movement.Launch(Vector2.zero);

        if (_isAirJump)
        {
            mushroom.SpawnAirJumpDust();
        }
    }
}

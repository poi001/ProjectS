using UnityEngine;

// 플라이. 대기 높이에서 일정한 속도(FlySpeed)로 좌우로 왔다 갔다 한다. 다른 상태로 넘어가기 전까지 계속 유지된다.
// 왕복 범위의 끝(FlyMinX, FlyMaxX)에 닿으면 방향을 바꾼다. 처음에는 화면 가운데 쪽으로 출발한다.
// 들어온 뒤 최소 시간(FlyMinDuration) 동안은 무조건 플라이다. 그 시간이 지나면 다음 패턴을 무작위로 하나 고르고, 그 조건이 될 때까지 계속 왕복한다.
//  - 스매시(SmashChance의 확률): 플레이어가 수직 아래쯤에 오면(Mossback.IsOverTarget) 스매시 시작으로 넘어간다.
//  - 샷(나머지): 왼쪽과 오른쪽 중 하나를 무작위로 고르고, 그쪽 왕복 끝에 닿으면 샷으로 넘어간다.
// 날아가는 동안에는 몸에 닿아도 피해가 없다.
// 애니메이션: Fly(Enemy3-Fly 시트, 반복)
public class MossbackFlyState : MossbackState
{
    private enum NextPattern
    {
        // 아직 고르지 않음 (최소 시간이 지나기 전)
        None,
        Smash,
        Shot,
    }

    // 왕복할 수 있다고 보는 최소 범위 폭. 이보다 좁으면 움직이지 않는다. (단위: 월드 거리)
    private const float MinFlyRange = 0.01f;

    // 날아가는 방향 (오른쪽 1, 왼쪽 -1)
    private float _direction;
    private float _enterTime;
    private NextPattern _nextPattern;
    // 샷을 쏠 쪽 (오른쪽 끝 1, 왼쪽 끝 -1). 샷을 골랐을 때만 쓴다.
    private float _shotSide;

    public MossbackFlyState(Mossback owner, StateMachine<Enemy> stateMachine)
        : base(owner, stateMachine)
    {
    }

    public override void Enter()
    {
        _direction = mossback.Movement.Position.x >= mossback.FieldCenterX ? -1f : 1f;
        _enterTime = Time.time;
        _nextPattern = NextPattern.None;

        mossback.Animation.UpdateFacing(_direction);
        mossback.Animation.Play(MossbackAnimation.Fly);
    }

    // 플라이가 중간에 끊기는 경우(플레이어 사망, Mossback 사망 등)에도 그 자리에 멈춘다.
    public override void Exit()
    {
        mossback.Movement.SetVelocity(Vector2.zero);
    }

    public override void LogicUpdate()
    {
        if (_nextPattern == NextPattern.None)
        {
            if (Time.time - _enterTime < mossback.FlyMinDuration)
            {
                return;
            }

            ChooseNextPattern();
        }

        if (_nextPattern == NextPattern.Smash && mossback.IsOverTarget)
        {
            stateMachine.ChangeState(mossback.SmashStartState);
        }
    }

    public override void PhysicsUpdate()
    {
        // 벽 사이가 좁아 왕복 범위가 한 점으로 모였으면 제자리에 떠 있는다. (매 스텝 방향이 뒤집혀 떨리지 않게 한다)
        if (mossback.FlyMaxX - mossback.FlyMinX <= MinFlyRange)
        {
            mossback.Movement.SetVelocity(Vector2.zero);

            // 왕복 끝이 따로 없으므로 샷은 그 자리에서 바로 쏜다.
            if (_nextPattern == NextPattern.Shot)
            {
                stateMachine.ChangeState(mossback.ShotState);
            }

            return;
        }

        float positionX = mossback.Movement.Position.x;

        // 가는 쪽 끝에 닿았으면 샷을 쏠 쪽인지 확인하고, 아니면 돌아선다.
        if ((_direction > 0f && positionX >= mossback.FlyMaxX) || (_direction < 0f && positionX <= mossback.FlyMinX))
        {
            if (_nextPattern == NextPattern.Shot && Mathf.Approximately(_direction, _shotSide))
            {
                stateMachine.ChangeState(mossback.ShotState);
                return;
            }

            _direction = -_direction;
            mossback.Animation.UpdateFacing(_direction);
        }

        mossback.Movement.SetVelocity(new Vector2(_direction * mossback.FlySpeed, 0f));
    }

    // 다음 패턴을 고른다. 스매시와 샷 중 하나이며, 샷이면 쏠 쪽(왼쪽/오른쪽)도 반반으로 고른다.
    private void ChooseNextPattern()
    {
        // Random.value는 1도 나올 수 있으므로 확률 1(항상 스매시)은 따로 본다.
        if (mossback.SmashChance >= 1f || Random.value < mossback.SmashChance)
        {
            _nextPattern = NextPattern.Smash;
            return;
        }

        _nextPattern = NextPattern.Shot;
        _shotSide = Random.value < 0.5f ? -1f : 1f;
    }
}

using UnityEngine;

// BloodWing 이동 상태들(브레스, 스킬의 가운데·원래 자리 이동, 대시 복귀)이 함께 쓰는 직선 이동.
// 정해진 시간 동안 출발 지점에서 도착 지점까지 일정한 속도로 Rigidbody를 옮긴다. 상태의 PhysicsUpdate에서 매 물리 프레임 Step을 호출한다.
// 물리 스텝마다 "한 스텝 뒤에 있어야 할 지점"을 향해 속도를 정하므로, 지형에 스쳐 밀려나도 다음 스텝에 경로로 돌아오고 도착 지점에 정확히 멈춘다.
// 시작 시각은 첫 Step(첫 물리 프레임)에서 정한다. 상태에는 Update에서 들어오므로 그 시각에 정하면 첫 스텝의 속도가 튄다.
public class BloodWingLinearMove
{
    private Vector2 _from;
    private Vector2 _to;
    private float _duration;
    // 이동을 시작한 물리 시각. 아직 정하지 않았으면 음수
    private float _startTime;

    // 새 이동을 준비한다. duration은 0보다 커야 한다. (단위: 초)
    public void Begin(Vector2 from, Vector2 to, float duration)
    {
        _from = from;
        _to = to;
        _duration = duration;
        _startTime = -1f;
    }

    // 한 물리 스텝만큼 이동한다. 도착했으면 속도를 0으로 하고 true를 반환한다.
    public bool Step(CharacterMovement movement)
    {
        if (_startTime < 0f)
        {
            _startTime = Time.fixedTime;
        }

        float elapsed = Time.fixedTime - _startTime;

        if (elapsed >= _duration)
        {
            movement.SetVelocity(Vector2.zero);
            return true;
        }

        float nextProgress = Mathf.Clamp01((elapsed + Time.fixedDeltaTime) / _duration);
        Vector2 nextPoint = Vector2.Lerp(_from, _to, nextProgress);
        movement.SetVelocity((nextPoint - movement.Position) / Time.fixedDeltaTime);
        return false;
    }
}

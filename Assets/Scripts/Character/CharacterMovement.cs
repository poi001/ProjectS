using UnityEngine;

// 플레이어/적 공용. 상태가 요청한 이동/점프 물리 연산과 접지·벽 감지만 담당한다.
// 점프 입력 버퍼, 코요테 타임처럼 입력에 관련된 판단은 각 캐릭터(예: Player)가 담당한다.
// 플레이어는 이동속도 스탯으로 Move를, 적은 이동속도 스탯 없이 패턴이 정한 속도로 SetVelocityX를 쓴다. (GDD 6.5)
// 상태(FixedUpdate)보다 먼저 접지 정보를 갱신하도록 실행 순서를 앞당긴다. (GroundChecker보다는 뒤)
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(Rigidbody2D), typeof(GroundChecker))]
public class CharacterMovement : MonoBehaviour
{
    private const string WallLayerName = "Wall";
    private const string GroundLayerName = "Ground";
    // 벽/지형에 닿았는지 확인할 때 콜라이더 가장자리에서 더 검사하는 거리
    private const float WallCheckSkin = 0.05f;
    // IsBlocked는 박스캐스트라 콜라이더 자체의 여유 때문에 WallCheckSkin보다 약간 먼 거리에서도 true가 된다.
    // 막혀서 멈춘 자리에서 벽 접촉이 false로 나오지 않도록 레이캐스트에 더하는 여유 거리
    private const float WallTouchMargin = 0.02f;
    // 옆이 막혔는지 확인할 때 판정 박스 높이 비율. 발밑 바닥과 머리 위 천장에 닿은 것은 막힌 것으로 보지 않도록 줄인다.
    private const float BlockCheckHeightRatio = 0.8f;
    // 목표 지점을 향한 점프의 최소 상승 높이. 목표가 현재 높이와 같거나 낮아도 이만큼은 뛰어오른다. (단위: 월드 거리)
    private const float MinTargetJumpHeight = 0.5f;
    // 가장 가까운 벽을 찾을 때 좌우로 검사하는 최대 거리
    private const float WallSearchDistance = 100f;

    [Header("점프")]
    [SerializeField] private float _jumpHeight = 3f;
    // 점프 직후 판정 박스가 아직 바닥과 겹쳐 접지로 오판정되는 것을 막는 시간
    [SerializeField] private float _groundIgnoreTimeAfterJump = 0.1f;

    private Rigidbody2D _rigidbody;
    private GroundChecker _groundChecker;
    // 루트의 바닥/벽 충돌용 콜라이더 (GDD 6.7)
    private Collider2D _bodyCollider;
    private int _wallLayerMask;
    // 옆을 막을 수 있는 지형 (벽, 발판 옆면)
    private int _obstacleLayerMask;
    // 중력을 껐다 켤 때(예: 벽에 매달리기) 되돌릴 원래 중력 배율
    private float _defaultGravityScale;
    // 이동속도는 각 캐릭터가 스탯에서 받아 SetMoveSpeed로 설정한다.
    private float _moveSpeed;
    private float _lastGroundedTime = float.NegativeInfinity;
    private float _groundIgnoreEndTime = float.NegativeInfinity;

    public bool IsGrounded { get; private set; }
    public Vector2 Velocity => _rigidbody.linearVelocity;

    // 물리 기준 위치. Rigidbody2D가 Interpolate면 transform.position은 물리 위치보다 한 스텝 뒤처지므로,
    // 물리 판정(Bounds, 속도 계산)과 함께 쓰는 위치는 이 값을 쓴다.
    public Vector2 Position => _rigidbody.position;

    // 마지막으로 접지해 있던 시점부터 지난 시간. 점프로 땅을 떠나면 무한대가 된다. (코요테 타임 판정용)
    public float TimeSinceGrounded => Time.time - _lastGroundedTime;

    // 점프해서 최고점에 닿기까지 걸리는 시간. 같은 높이에 착지한다면 체공 시간은 이 값의 2배다.
    public float JumpApexTime => CalculateJumpVelocity() / Gravity;

    private float Gravity => Mathf.Abs(Physics2D.gravity.y * _defaultGravityScale);

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _groundChecker = GetComponent<GroundChecker>();
        _bodyCollider = GetComponent<Collider2D>();
        _wallLayerMask = LayerMask.GetMask(WallLayerName);
        _obstacleLayerMask = LayerMask.GetMask(WallLayerName, GroundLayerName);
        _defaultGravityScale = _rigidbody.gravityScale;
    }

    private void FixedUpdate()
    {
        UpdateGrounded();
    }

    public void SetMoveSpeed(float moveSpeed)
    {
        _moveSpeed = moveSpeed;
    }

    public void Move(float direction)
    {
        SetVelocityX(direction * _moveSpeed);
    }

    // 이동속도 스탯과 관계없이 가로 속도를 직접 정한다. (적의 패턴 이동용)
    public void SetVelocityX(float velocityX)
    {
        Vector2 velocity = _rigidbody.linearVelocity;
        velocity.x = velocityX;
        _rigidbody.linearVelocity = velocity;
    }

    public void SetVelocity(Vector2 velocity)
    {
        _rigidbody.linearVelocity = velocity;
    }

    public void SetGravityEnabled(bool isEnabled)
    {
        _rigidbody.gravityScale = isEnabled ? _defaultGravityScale : 0f;
    }

    public void Jump()
    {
        Jump(CalculateJumpVelocity());
    }

    // 세로 속도를 직접 정해 점프한다. (목표 지점을 향한 점프용, CalculateJumpVelocityTo로 구한 값을 넘긴다)
    public void Jump(float velocityY)
    {
        Vector2 velocity = _rigidbody.linearVelocity;
        velocity.y = velocityY;
        _rigidbody.linearVelocity = velocity;

        // 코요테 타임을 소모해 한 번의 접지로 중복 점프하지 않도록 한다.
        _lastGroundedTime = float.NegativeInfinity;
        _groundIgnoreEndTime = Time.time + _groundIgnoreTimeAfterJump;
        IsGrounded = false;
    }

    // 기본 점프 높이의 heightRatio배 높이까지 오르는 점프의 세로 속도. (공중 점프, 벽 점프용. Jump(float)에 넘긴다)
    public float GetJumpVelocity(float heightRatio)
    {
        return Mathf.Sqrt(2f * Gravity * _jumpHeight * Mathf.Max(0f, heightRatio));
    }

    // 속도를 정해 공중으로 띄운다. (피격 넉백용) 점프처럼 코요테 타임을 소모하고, 직후 판정 박스가 바닥과 겹쳐 접지로 오판정되는 것을 막는다.
    public void Launch(Vector2 velocity)
    {
        _rigidbody.linearVelocity = velocity;

        _lastGroundedTime = float.NegativeInfinity;
        _groundIgnoreEndTime = Time.time + _groundIgnoreTimeAfterJump;
        IsGrounded = false;
    }

    // 해당 방향(오른쪽 양수, 왼쪽 음수)으로 벽에 붙어 있는지 확인한다.
    // IsBlocked로 막혀 멈춘 자리에서도 true가 되도록 여유 거리(WallTouchMargin)를 더해 검사한다.
    public bool IsTouchingWall(float direction)
    {
        Bounds bounds = _bodyCollider.bounds;
        Vector2 rayDirection = new Vector2(Mathf.Sign(direction), 0f);
        float rayDistance = bounds.extents.x + WallCheckSkin + WallTouchMargin;
        RaycastHit2D hit = Physics2D.Raycast(bounds.center, rayDirection, rayDistance, _wallLayerMask);
        return hit.collider != null;
    }

    // 해당 방향(오른쪽 양수, 왼쪽 음수)이 지형(벽, 발판 옆면)에 막혀 있는지 확인한다.
    public bool IsBlocked(float direction)
    {
        Bounds bounds = _bodyCollider.bounds;
        Vector2 checkSize = new Vector2(bounds.size.x, bounds.size.y * BlockCheckHeightRatio);
        Vector2 castDirection = new Vector2(Mathf.Sign(direction), 0f);
        RaycastHit2D hit = Physics2D.BoxCast(bounds.center, checkSize, 0f, castDirection, WallCheckSkin, _obstacleLayerMask);
        return hit.collider != null;
    }

    // origin에서 해당 방향(오른쪽 양수, 왼쪽 음수)으로 maxDistance 안에 벽이 있으면 벽까지의 거리를, 없으면 maxDistance를 반환한다.
    public float GetFreeDistanceToWall(Vector2 origin, float direction, float maxDistance)
    {
        Vector2 rayDirection = new Vector2(Mathf.Sign(direction), 0f);
        RaycastHit2D hit = Physics2D.Raycast(origin, rayDirection, maxDistance, _wallLayerMask);
        return hit.collider != null ? hit.distance : maxDistance;
    }

    // 좌우 중 가장 가까운 벽을 찾는다. distance는 콜라이더 가장자리에서 벽까지의 거리다.
    public bool TryFindNearestWall(out float direction, out float distance)
    {
        Bounds bounds = _bodyCollider.bounds;
        RaycastHit2D rightHit = Physics2D.Raycast(bounds.center, Vector2.right, WallSearchDistance, _wallLayerMask);
        RaycastHit2D leftHit = Physics2D.Raycast(bounds.center, Vector2.left, WallSearchDistance, _wallLayerMask);
        bool hasRight = rightHit.collider != null;
        bool hasLeft = leftHit.collider != null;

        if (!hasRight && !hasLeft)
        {
            direction = 0f;
            distance = 0f;
            return false;
        }

        bool isRightNearer = hasRight && (!hasLeft || rightHit.distance <= leftHit.distance);
        RaycastHit2D nearestHit = isRightNearer ? rightHit : leftHit;
        direction = isRightNearer ? 1f : -1f;
        distance = Mathf.Max(0f, nearestHit.distance - bounds.extents.x);
        return true;
    }

    private void UpdateGrounded()
    {
        IsGrounded = _groundChecker.IsGrounded && Time.time >= _groundIgnoreEndTime;

        if (IsGrounded)
        {
            _lastGroundedTime = Time.time;
        }
    }

    // 현재 위치에서 가로로 horizontalDistance(0 이상), 세로로 heightGain만큼 떨어진 지점을 정확히 지나가는 점프 속도를 구한다.
    // 반환값의 x는 가로 속도의 크기(방향은 호출한 쪽이 정한다), y는 Jump(float)에 넘길 세로 속도다.
    // 기본은 최고점에서 그 지점에 닿는 시간(sqrt(2h/g))으로 날아가고, 그러면 가로 속도가 maxSpeedX를 넘을 때는
    // 비행 시간을 늘려 최대 속도로 날아간다. (내려오면서 그 지점을 지난다)
    // 비행 시간은 물리 스텝(Time.fixedDeltaTime)의 정수배로 맞춘다.
    // 물리 엔진은 한 스텝마다 속도에 중력을 더한 뒤 위치를 옮기므로(반암시적 오일러),
    // N스텝 뒤 높이가 heightGain이 되는 세로 속도는 v = h / T + g * (T + dt) / 2 다. (T = N * dt)
    public Vector2 CalculateJumpVelocityTo(float horizontalDistance, float heightGain, float maxSpeedX)
    {
        float height = Mathf.Max(heightGain, MinTargetJumpHeight);
        float flightTime = Mathf.Sqrt(2f * height / Gravity);

        if (maxSpeedX > 0f)
        {
            flightTime = Mathf.Max(flightTime, horizontalDistance / maxSpeedX);
        }

        float stepTime = Time.fixedDeltaTime;
        flightTime = Mathf.Ceil(flightTime / stepTime) * stepTime;

        float velocityX = horizontalDistance / flightTime;
        float velocityY = height / flightTime + Gravity * (flightTime + stepTime) * 0.5f;
        return new Vector2(velocityX, velocityY);
    }

    // 목표 점프 높이에 도달하기 위한 초기 속도: v = sqrt(2 * g * h)
    // 중력을 잠시 끈 상태에서도 같은 값이 나오도록 원래 중력 배율로 계산한다.
    private float CalculateJumpVelocity()
    {
        return Mathf.Sqrt(2f * Gravity * _jumpHeight);
    }
}

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
    // 옆이 막혔는지 확인할 때 판정 박스 높이 비율. 발밑 바닥과 머리 위 천장에 닿은 것은 막힌 것으로 보지 않도록 줄인다.
    private const float BlockCheckHeightRatio = 0.8f;
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
        Vector2 velocity = _rigidbody.linearVelocity;
        velocity.y = CalculateJumpVelocity();
        _rigidbody.linearVelocity = velocity;

        // 코요테 타임을 소모해 한 번의 접지로 중복 점프하지 않도록 한다.
        _lastGroundedTime = float.NegativeInfinity;
        _groundIgnoreEndTime = Time.time + _groundIgnoreTimeAfterJump;
        IsGrounded = false;
    }

    // 해당 방향(오른쪽 양수, 왼쪽 음수)으로 벽에 붙어 있는지 확인한다.
    public bool IsTouchingWall(float direction)
    {
        Bounds bounds = _bodyCollider.bounds;
        Vector2 rayDirection = new Vector2(Mathf.Sign(direction), 0f);
        RaycastHit2D hit = Physics2D.Raycast(bounds.center, rayDirection, bounds.extents.x + WallCheckSkin, _wallLayerMask);
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

    // 목표 점프 높이에 도달하기 위한 초기 속도: v = sqrt(2 * g * h)
    // 중력을 잠시 끈 상태에서도 같은 값이 나오도록 원래 중력 배율로 계산한다.
    private float CalculateJumpVelocity()
    {
        return Mathf.Sqrt(2f * Gravity * _jumpHeight);
    }
}

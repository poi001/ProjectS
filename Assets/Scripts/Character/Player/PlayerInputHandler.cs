using UnityEngine;
using UnityEngine.InputSystem;

// New Input System(PlayerInputActions)으로부터 키 입력을 받고, 점프/공격/대시 입력을 버퍼에 보관한다.
// 버퍼: 입력이 조금 일찍 들어와도 버퍼 시간 안에 실행 가능해지면 처리되도록 마지막 입력 시각을 기억한다.
// 입력 잠금(SetLocked): 시네마틱처럼 조작을 받지 않아야 할 때 켠다. 잠근 동안 이동 입력은 0으로 읽히고 점프/공격/대시 입력은 버려진다.
// 키 입력 자체는 계속 받아 두므로, 방향키를 누른 채로 잠금이 풀리면 다시 누르지 않아도 바로 움직인다.
public class PlayerInputHandler : MonoBehaviour
{
    [Header("입력 버퍼")]
    [SerializeField] private float _jumpBufferTime = 0.25f;
    [SerializeField] private float _attackBufferTime = 0.25f;
    [Tooltip("대시 입력을 기억해 두는 시간입니다. 쿨타임이 끝난 뒤 누른 입력만 유효하며, 대시 중이나 쿨타임 중 누른 입력은 다음 대시로 예약되지 않습니다. (단위: 초)")]
    [SerializeField] private float _dashBufferTime = 0.25f;

    private PlayerInputActions _inputActions;
    private float _lastJumpPressedTime = float.NegativeInfinity;
    private float _lastAttackPressedTime = float.NegativeInfinity;
    private float _lastDashPressedTime = float.NegativeInfinity;

    // 실제로 눌려 있는 이동 입력. 잠금 중에도 갱신해 두었다가 잠금이 풀리면 그대로 쓴다.
    private float _rawMoveInput;

    // 입력 잠금 여부. 잠근 동안에는 어떤 조작도 받지 않는다.
    public bool IsLocked { get; private set; }

    public float MoveInput => IsLocked ? 0f : _rawMoveInput;

    // 점프 버퍼 시간 안에 눌린 점프 입력이 아직 소비되지 않고 남아 있는지 여부
    public bool HasBufferedJump => Time.time - _lastJumpPressedTime <= _jumpBufferTime;

    // 공격 버퍼 시간 안에 눌린 공격 입력이 아직 소비되지 않고 남아 있는지 여부
    public bool HasBufferedAttack => IsAttackBuffered(0f);

    private void Awake()
    {
        _inputActions = new PlayerInputActions();
    }

    private void OnEnable()
    {
        _inputActions.Player.Move.performed += OnMove;
        _inputActions.Player.Move.canceled += OnMove;
        _inputActions.Player.Jump.started += OnJump;
        _inputActions.Player.Attack.started += OnAttack;
        _inputActions.Player.Dash.started += OnDash;
        _inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        _inputActions.Player.Disable();
        _inputActions.Player.Move.performed -= OnMove;
        _inputActions.Player.Move.canceled -= OnMove;
        _inputActions.Player.Jump.started -= OnJump;
        _inputActions.Player.Attack.started -= OnAttack;
        _inputActions.Player.Dash.started -= OnDash;
        _rawMoveInput = 0f;
        ConsumeJumpBuffer();
        ConsumeAttackBuffer();
        ConsumeDashBuffer();
    }

    private void OnDestroy()
    {
        _inputActions.Dispose();
    }

    // 입력을 잠그거나 푼다. 잠그는 순간 버퍼에 남은 점프/공격/대시 입력도 버려서, 잠금이 풀린 뒤에 뒤늦게 실행되지 않게 한다.
    public void SetLocked(bool isLocked)
    {
        if (IsLocked == isLocked)
        {
            return;
        }

        IsLocked = isLocked;

        if (isLocked)
        {
            ConsumeJumpBuffer();
            ConsumeAttackBuffer();
            ConsumeDashBuffer();
        }
    }

    // 점프 입력을 소비한다. 한 번의 입력으로 중복 점프하지 않도록 한다.
    public void ConsumeJumpBuffer()
    {
        _lastJumpPressedTime = float.NegativeInfinity;
    }

    // 판정 시점이 기준 시점(예: 애니메이션 종료)보다 늦어진 만큼(extraTime) 버퍼 시간을 늘려서 확인한다.
    public bool IsAttackBuffered(float extraTime)
    {
        return Time.time - _lastAttackPressedTime <= _attackBufferTime + extraTime;
    }

    // 공격 입력을 소비한다. 하나의 입력이 공격 시작과 다음 콤보에 중복으로 쓰이지 않도록 한다.
    public void ConsumeAttackBuffer()
    {
        _lastAttackPressedTime = float.NegativeInfinity;
    }

    // 대시 입력을 소비한다. 한 번의 입력으로 중복 대시하지 않도록 한다.
    public void ConsumeDashBuffer()
    {
        _lastDashPressedTime = float.NegativeInfinity;
    }

    // 대시 사용 가능 시각 이후에 누른 입력만 인정한다. 쿨타임 중 입력이 종료 직후 발동되는 것을 막는다.
    public bool IsDashBuffered(float earliestPressTime)
    {
        return _lastDashPressedTime >= earliestPressTime && Time.time - _lastDashPressedTime <= _dashBufferTime;
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        _rawMoveInput = context.ReadValue<float>();
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        if (IsLocked)
        {
            return;
        }

        _lastJumpPressedTime = Time.time;
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        if (IsLocked)
        {
            return;
        }

        _lastAttackPressedTime = Time.time;
    }

    private void OnDash(InputAction.CallbackContext context)
    {
        if (IsLocked)
        {
            return;
        }

        _lastDashPressedTime = Time.time;
    }
}

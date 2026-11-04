using UnityEngine;
using UnityEngine.InputSystem;

// New Input System(PlayerInputActions)으로부터 키 입력을 받고, 점프/공격 입력을 버퍼에 보관한다.
// 버퍼: 입력이 조금 일찍 들어와도 버퍼 시간 안에 실행 가능해지면 처리되도록 마지막 입력 시각을 기억한다.
public class PlayerInputHandler : MonoBehaviour
{
    [Header("입력 버퍼")]
    [SerializeField] private float _jumpBufferTime = 0.25f;
    [SerializeField] private float _attackBufferTime = 0.25f;

    private PlayerInputActions _inputActions;
    private float _lastJumpPressedTime = float.NegativeInfinity;
    private float _lastAttackPressedTime = float.NegativeInfinity;

    public float MoveInput { get; private set; }

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
        _inputActions.Player.Enable();
    }

    private void OnDisable()
    {
        _inputActions.Player.Disable();
        _inputActions.Player.Move.performed -= OnMove;
        _inputActions.Player.Move.canceled -= OnMove;
        _inputActions.Player.Jump.started -= OnJump;
        _inputActions.Player.Attack.started -= OnAttack;
        MoveInput = 0f;
        ConsumeJumpBuffer();
        ConsumeAttackBuffer();
    }

    private void OnDestroy()
    {
        _inputActions.Dispose();
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

    private void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<float>();
    }

    private void OnJump(InputAction.CallbackContext context)
    {
        _lastJumpPressedTime = Time.time;
    }

    private void OnAttack(InputAction.CallbackContext context)
    {
        _lastAttackPressedTime = Time.time;
    }
}

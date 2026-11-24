using MoreMountains.Feedbacks;
using UnityEngine;

// 플레이어 전용 애니메이션. 애니메이션 상태 이름(해시)과 콤보 등록을 담당한다.
// 공격 애니메이션(Combo{n}, Combo{n}_End, JumpAttack{n}, JumpAttack{n}_End) 상태의 Speed Multiplier는 AttackSpeed 파라미터에 연결한다. (CharacterAnimation.SetAttackSpeed)
public class PlayerAnimation : CharacterAnimation
{
    public static readonly int Idle = Animator.StringToHash("Idle");
    public static readonly int Run = Animator.StringToHash("Run");
    // 뛰어오르는 동작. 끝나면 공중 자세(Fall)로 넘어간다.
    public static readonly int JumpStart = Animator.StringToHash("JumpStart");
    public static readonly int Fall = Animator.StringToHash("Fall");
    public static readonly int Hit = Animator.StringToHash("Hit");
    public static readonly int WallSlide = Animator.StringToHash("WallSlide");
    public static readonly int WallJump = Animator.StringToHash("WallJump");
    public static readonly int Dash = Animator.StringToHash("Dash");
    public static readonly int Block = Animator.StringToHash("Block");
    public static readonly int Skill1 = Animator.StringToHash("Skill1");
    public static readonly int Heal = Animator.StringToHash("Heal");
    public static readonly int Dead = Animator.StringToHash("Dead");

    [Header("벽타기 연출")]
    [Tooltip("벽타기 중 손의 먼지를 표시하는 Feel 피드백입니다. Play에서 먼지 오브젝트를 켜고 Stop에서 끄도록 구성합니다. 초기화 방식은 Script로 둡니다.")]
    [SerializeField] private MMF_Player _wallSlideFeedbacks;

    [Header("대시 연출")]
    [Tooltip("대시를 시작할 때 몸 뒤의 속도선을 켜는 피드백입니다. Visuals/Dash/DashLines_Enter의 MMF_Player를 연결합니다. 초기화 방식은 Script로 두며, 초기화할 때 속도선을 꺼 둡니다.\n비워 두면 속도선이 나오지 않습니다.")]
    [SerializeField] private MMF_Player _dashLinesEnter;
    [Tooltip("대시가 끝나거나 중간에 끊길 때 속도선을 끄는 피드백입니다. Visuals/Dash/DashLines_Exit의 MMF_Player를 연결합니다.")]
    [SerializeField] private MMF_Player _dashLinesExit;

    private int[] _comboHashes = new int[0];
    private int[] _comboEndHashes = new int[0];
    private int[] _airComboHashes = new int[0];
    private int[] _airComboEndHashes = new int[0];

    public override void Initialize()
    {
        base.Initialize();

        if (_wallSlideFeedbacks != null)
        {
            _wallSlideFeedbacks.Initialization();
        }

        if (_dashLinesEnter != null)
        {
            _dashLinesEnter.Initialization();
        }

        if (_dashLinesExit != null)
        {
            _dashLinesExit.Initialization();
        }
    }

    public void PlayDashLines()
    {
        if (_dashLinesEnter != null)
        {
            _dashLinesEnter.PlayFeedbacks();
        }
    }

    public void StopDashLines()
    {
        if (_dashLinesExit != null)
        {
            _dashLinesExit.PlayFeedbacks();
        }
    }

    public void PlayWallSlideDust()
    {
        if (_wallSlideFeedbacks != null)
        {
            _wallSlideFeedbacks.PlayFeedbacks();
        }
    }

    public void StopWallSlideDust()
    {
        if (_wallSlideFeedbacks != null)
        {
            _wallSlideFeedbacks.StopFeedbacks();
        }
    }

    private void OnDisable()
    {
        StopWallSlideDust();
    }

    // 지상 콤보 애니메이션 상태를 "Combo{n}", "Combo{n}_End" 이름 규칙으로 찾아 등록하고, 실제로 사용할 수 있는 콤보 수를 반환한다.
    public int SetupCombos(int requestedCount)
    {
        return FindComboStates("Combo", requestedCount, out _comboHashes, out _comboEndHashes);
    }

    // 공중 콤보 애니메이션 상태를 "JumpAttack{n}", "JumpAttack{n}_End" 이름 규칙으로 찾아 등록하고, 실제로 사용할 수 있는 콤보 수를 반환한다.
    public int SetupAirCombos(int requestedCount)
    {
        return FindComboStates("JumpAttack", requestedCount, out _airComboHashes, out _airComboEndHashes);
    }

    public int GetComboHash(int comboIndex)
    {
        return _comboHashes[comboIndex];
    }

    public int GetComboEndHash(int comboIndex)
    {
        return _comboEndHashes[comboIndex];
    }

    public int GetAirComboHash(int comboIndex)
    {
        return _airComboHashes[comboIndex];
    }

    public int GetAirComboEndHash(int comboIndex)
    {
        return _airComboEndHashes[comboIndex];
    }

    // "{prefix}{n}"와 "{prefix}{n}_End" 상태를 1번부터 순서대로 찾는다. 둘 중 하나라도 없으면 그 앞 단계까지만 쓰고, 찾은 단계 수를 반환한다.
    private int FindComboStates(string prefix, int requestedCount, out int[] comboHashes, out int[] comboEndHashes)
    {
        comboHashes = new int[requestedCount];
        comboEndHashes = new int[requestedCount];
        int availableCount = 0;

        for (int i = 0; i < requestedCount; i++)
        {
            string comboName = $"{prefix}{i + 1}";
            string comboEndName = $"{comboName}_End";
            int comboHash = Animator.StringToHash(comboName);
            int comboEndHash = Animator.StringToHash(comboEndName);

            if (!animator.HasState(0, comboHash) || !animator.HasState(0, comboEndHash))
            {
                Debug.LogError($"Animator에 '{comboName}' 또는 '{comboEndName}' 상태가 없어 {prefix} 콤보를 {availableCount}개까지만 사용합니다.", this);
                break;
            }

            comboHashes[i] = comboHash;
            comboEndHashes[i] = comboEndHash;
            availableCount++;
        }

        return availableCount;
    }
}

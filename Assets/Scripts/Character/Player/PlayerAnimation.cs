using UnityEngine;

// 플레이어 전용 애니메이션. 애니메이션 상태 이름(해시)과 콤보 등록을 담당한다.
// 공격 애니메이션(Combo{n}, Combo{n}_End, JumpAttack, JumpAttack_End) 상태의 Speed Multiplier는 AttackSpeed 파라미터에 연결한다. (CharacterAnimation.SetAttackSpeed)
public class PlayerAnimation : CharacterAnimation
{
    public static readonly int Idle = Animator.StringToHash("Idle");
    public static readonly int Run = Animator.StringToHash("Run");
    public static readonly int Jump = Animator.StringToHash("Jump");
    public static readonly int FallLoop = Animator.StringToHash("Fall_Loop");
    public static readonly int JumpAttack = Animator.StringToHash("JumpAttack");
    public static readonly int JumpAttackEnd = Animator.StringToHash("JumpAttack_End");
    public static readonly int Hit = Animator.StringToHash("Hit");
    public static readonly int Dead = Animator.StringToHash("Dead");

    private int[] _comboHashes = new int[0];
    private int[] _comboEndHashes = new int[0];

    // 콤보 애니메이션 상태를 "Combo{n}", "Combo{n}_End" 이름 규칙으로 찾아 등록하고, 실제로 사용할 수 있는 콤보 수를 반환한다.
    // 캐릭터마다 Animator(Override Controller)가 달라도 같은 규칙의 상태 이름만 있으면 콤보 수를 자유롭게 정할 수 있다.
    public int SetupCombos(int requestedCount)
    {
        _comboHashes = new int[requestedCount];
        _comboEndHashes = new int[requestedCount];
        int availableCount = 0;

        for (int i = 0; i < requestedCount; i++)
        {
            string comboName = $"Combo{i + 1}";
            string comboEndName = $"{comboName}_End";
            int comboHash = Animator.StringToHash(comboName);
            int comboEndHash = Animator.StringToHash(comboEndName);

            if (!animator.HasState(0, comboHash) || !animator.HasState(0, comboEndHash))
            {
                Debug.LogError($"Animator에 '{comboName}' 또는 '{comboEndName}' 상태가 없어 콤보를 {availableCount}개까지만 사용합니다.", this);
                break;
            }

            _comboHashes[i] = comboHash;
            _comboEndHashes[i] = comboEndHash;
            availableCount++;
        }

        return availableCount;
    }

    public int GetComboHash(int comboIndex)
    {
        return _comboHashes[comboIndex];
    }

    public int GetComboEndHash(int comboIndex)
    {
        return _comboEndHashes[comboIndex];
    }
}

using UnityEngine;

// 이 캐릭터가 피해를 입을 때 피격음을 재생한다. 피해를 입는 캐릭터(Knight 등)의 프리팹 루트에 붙인다.
// 같은 오브젝트의 CharacterVital.Damaged 이벤트를 구독하므로 대상 필터링이 필요 없고, 캐릭터마다 SoundDataSO만 바꿔 끼우면 된다.
// 치명 일격도 피격음을 재생하되, 일반 피격음을 생략할 게이지가 꽉 차 있으면 재생하지 않는다.
// 단, 페이즈를 끝내는 일격(체력을 모두 잃었지만 다음 페이즈로 넘어가는 피격)은 게이지가 꽉 차 있어도 재생한다.
// 이 일격으로는 기절하지 않고(기절음 없음) 사망도 아니라서(KO 사운드 없음) 생략하면 소리가 하나도 나지 않기 때문이다. 페이즈가 둘 이상인 보스에 모두 적용된다.
// 피해 무효 중에 맞으면(CharacterVital.DamageNullified) 일반 피격음 대신 피해 무효 피격음을 재생한다.
[RequireComponent(typeof(CharacterVital))]
public class HitSoundPlayer : MonoBehaviour
{
    [Tooltip("이 캐릭터가 피해를 입을 때마다 재생할 피격 소리 데이터입니다. 비워 두면 소리가 나지 않습니다.")]
    [SerializeField] private SoundDataSO _hitSound;
    [Tooltip("이 게이지가 꽉 찬 동안에는 일반 피격음을 생략합니다. Knight의 기절 효과음과 겹치지 않게 사용합니다. 비워 두면 게이지와 관계없이 재생합니다.")]
    [SerializeField] private PatternGauge _muteWhenGaugeFull;
    [Tooltip("피해 무효 중(SoulKnight의 기도 등)에 맞을 때 일반 피격음 대신 재생할 소리 데이터입니다. 비워 두면 소리가 나지 않습니다.")]
    [SerializeField] private SoundDataSO _nullifiedHitSound;

    private CharacterVital _vital;
    // 게이지가 꽉 찬 동안 일반 피격음을 생략하는 규칙을 쓰는지. 기본은 켜져 있다.
    private bool _isGaugeMuteEnabled = true;

    private void Awake()
    {
        _vital = GetComponent<CharacterVital>();
    }

    private void OnEnable()
    {
        _vital.Damaged += OnDamaged;
        _vital.DamageNullified += OnDamageNullified;
    }

    private void OnDisable()
    {
        _vital.Damaged -= OnDamaged;
        _vital.DamageNullified -= OnDamageNullified;
    }

    // 게이지가 꽉 찬 동안 일반 피격음을 생략하는 규칙을 켜거나 끈다.
    // 게이지가 꽉 차도 기절하지 않는 경우(SoulKnight 2페이즈 등)에는 겹칠 기절음이 없으므로 꺼서 피격음이 계속 나게 한다.
    public void SetGaugeMuteEnabled(bool isEnabled)
    {
        _isGaugeMuteEnabled = isEnabled;
    }

    private void OnDamageNullified(DamageInfo damageInfo)
    {
        if (_nullifiedHitSound == null || !SoundManager.HasInstance)
        {
            return;
        }

        SoundManager.Instance.PlaySfx(_nullifiedHitSound);
    }

    private void OnDamaged(DamageInfo damageInfo)
    {
        if (_hitSound == null || !SoundManager.HasInstance)
        {
            return;
        }

        // 이 피격으로 체력을 모두 잃었지만 다음 페이즈가 남아 있는지. (Damaged는 소진 상태가 시작되기 전에 호출되므로 이때 IsDead가 true다)
        bool endsPhase = _vital.IsDead && _vital.SurvivesDepletion;

        if (!endsPhase && _isGaugeMuteEnabled && _muteWhenGaugeFull != null && _muteWhenGaugeFull.IsFull)
        {
            return;
        }

        SoundManager.Instance.PlaySfx(_hitSound);
    }
}

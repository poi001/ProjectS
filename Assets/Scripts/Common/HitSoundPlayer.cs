using UnityEngine;

// 이 캐릭터가 피해를 입을 때 피격음을 재생한다. 피해를 입는 캐릭터(Knight 등)의 프리팹 루트에 붙인다.
// 같은 오브젝트의 CharacterVital.Damaged 이벤트를 구독하므로 대상 필터링이 필요 없고, 캐릭터마다 SoundDataSO만 바꿔 끼우면 된다.
// 치명 일격으로 죽는 타격도 Damaged가 먼저 발생하므로 피격음이 난다.
[RequireComponent(typeof(CharacterVital))]
public class HitSoundPlayer : MonoBehaviour
{
    [Tooltip("이 캐릭터가 피해를 입을 때마다 재생할 피격 소리 데이터입니다. 비워 두면 소리가 나지 않습니다.")]
    [SerializeField] private SoundDataSO _hitSound;

    private CharacterVital _vital;

    private void Awake()
    {
        _vital = GetComponent<CharacterVital>();
    }

    private void OnEnable()
    {
        _vital.Damaged += OnDamaged;
    }

    private void OnDisable()
    {
        _vital.Damaged -= OnDamaged;
    }

    private void OnDamaged(DamageInfo damageInfo)
    {
        if (_hitSound == null || !SoundManager.HasInstance)
        {
            return;
        }

        SoundManager.Instance.PlaySfx(_hitSound);
    }
}

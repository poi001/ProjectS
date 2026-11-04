using UnityEngine;

// 플레이어/적 공통 베이스. 공용 컴포넌트(이동, 애니메이션) 참조를 보관한다.
// 상태기계는 상태 타입이 캐릭터마다 다르므로 각 자식 클래스(Player, Enemy)가 직접 소유하고 구동한다.
// 체력/피격/사망은 공용 CharacterVital 컴포넌트가 담당한다. (각 캐릭터가 GetComponent로 참조한다.)
// 콜라이더 구성은 GDD 6.7을 따른다.
//  - 루트: Character 레이어의 Box Collider(트리거 아님). 바닥/벽 충돌 확인용이며 피격 판정에는 쓰지 않는다.
//  - 자식 "Collider": Player 또는 Enemy 레이어의 콜라이더(트리거). 피격 판정용이다.
[RequireComponent(typeof(CharacterMovement))]
public abstract class Character : MonoBehaviour
{
    private const string BodyLayerName = "Character";
    private const string HurtboxObjectName = "Collider";

    public CharacterMovement Movement { get; private set; }
    public CharacterAnimation Animation { get; private set; }
    // 루트의 바닥/벽 충돌용 콜라이더
    public Collider2D BodyCollider { get; private set; }
    // 자식 "Collider"의 피격 판정용 트리거 콜라이더
    public Collider2D Hurtbox { get; private set; }

    // 피격 판정 콜라이더가 속해야 하는 레이어 이름. 캐릭터 종류별로 정한다. (Player / Enemy)
    protected abstract string HurtboxLayerName { get; }

    // 자식 클래스에서 오버라이드할 때는 반드시 base.Awake()를 먼저 호출한다.
    protected virtual void Awake()
    {
        Movement = GetComponent<CharacterMovement>();
        Animation = GetComponentInChildren<CharacterAnimation>();
        Animation.Initialize();

        BodyCollider = GetComponent<Collider2D>();
        Hurtbox = FindHurtbox();
        ValidateColliders();
    }

    private Collider2D FindHurtbox()
    {
        Transform hurtboxTransform = transform.Find(HurtboxObjectName);

        if (hurtboxTransform == null)
        {
            return null;
        }

        return hurtboxTransform.GetComponent<Collider2D>();
    }

    // GDD 6.7의 콜라이더/레이어 규칙에서 벗어난 설정을 에러로 알린다.
    private void ValidateColliders()
    {
        if (BodyCollider == null)
        {
            Debug.LogError($"{name}: 루트에 바닥/벽 충돌용 Collider2D가 없습니다.", this);
        }
        else
        {
            if (BodyCollider.isTrigger)
            {
                Debug.LogError($"{name}: 루트 콜라이더는 트리거를 사용하지 않아야 합니다.", this);
            }

            if (BodyCollider.gameObject.layer != LayerMask.NameToLayer(BodyLayerName))
            {
                Debug.LogError($"{name}: 루트 콜라이더의 레이어는 {BodyLayerName}여야 합니다.", this);
            }
        }

        if (Hurtbox == null)
        {
            Debug.LogError($"{name}: 피격 판정용 자식 '{HurtboxObjectName}'(Collider2D)가 없습니다.", this);
            return;
        }

        if (!Hurtbox.isTrigger)
        {
            Debug.LogError($"{name}: 피격 판정 콜라이더는 트리거를 사용해야 합니다.", this);
        }

        if (Hurtbox.gameObject.layer != LayerMask.NameToLayer(HurtboxLayerName))
        {
            Debug.LogError($"{name}: 피격 판정 콜라이더의 레이어는 {HurtboxLayerName}여야 합니다.", this);
        }
    }
}

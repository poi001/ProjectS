using UnityEngine;

// 플레이어/적 공통 베이스. 공용 컴포넌트(이동, 애니메이션) 참조를 보관한다.
// 상태기계는 상태 타입이 캐릭터마다 다르므로 각 자식 클래스(Player, Enemy)가 직접 소유하고 구동한다.
// 체력/피격/사망은 공용 CharacterVital 컴포넌트가 담당한다. (각 캐릭터가 GetComponent로 참조한다.)
// 콜라이더 구성은 GDD 6.7을 따른다.
//  - 루트: Character 레이어의 Box Collider(트리거 아님). 바닥/벽 충돌 확인용이며 피격 판정에는 쓰지 않는다.
//  - 자식 "Collider": Player 또는 Enemy 레이어의 콜라이더(트리거). 피격 판정용이다.
// 공격 판정은 자식 오브젝트 없이 HitChecker(AttackHitChecker)가 공격 데이터의 범위(HitboxShape)로 처리한다.
// 캐릭터를 선택하면 자식 클래스가 그리는 공격 판정 범위가 Scene 뷰에 표시된다. (DrawHitboxGizmos)
// 초기화는 Awake/Start가 아니라 ICharacterLifecycle.Init으로 한다. (소환은 CharacterSpawner가 담당한다)
//  - Init: OnInit(참조·스탯·상태 준비와 설정 확인) → 모두 성공하면 OnInitCompleted(이벤트 구독·상태기계 시작)
//  - Release: OnRelease(이벤트 구독 해제). 오브젝트가 파괴될 때 아직 Release되지 않았으면 자동으로 호출한다.
//  - Init 전에는 자식 클래스가 Update/FixedUpdate에서 IsInitialized를 확인해 아무것도 하지 않는다.
[RequireComponent(typeof(CharacterMovement))]
public abstract class Character : MonoBehaviour, ICharacterLifecycle
{
    private const string BodyLayerName = "Character";
    private const string HurtboxObjectName = "Collider";
    // 디버그 표시를 켰을 때 판정이 나간 범위를 보여 주는 시간 (단위: 초)
    private const float HitRangeDisplayTime = 1f;

    [Header("디버그")]
    [Tooltip("켜면 플레이 중 공격 판정이 나간 범위를 붉은 박스로 1초 동안 보여 줍니다. Scene 뷰에서 보이며, Game 뷰에서는 Gizmos 버튼을 켜야 보입니다.\n범위를 눈으로 확인할 때만 켭니다. (캐릭터를 선택하면 모든 공격의 범위가 항상 선으로 보입니다.)")]
    [SerializeField] private bool _showHitRange;

    public CharacterMovement Movement { get; private set; }
    public CharacterAnimation Animation { get; private set; }
    // 루트의 바닥/벽 충돌용 콜라이더
    public Collider2D BodyCollider { get; private set; }
    // 자식 "Collider"의 피격 판정용 트리거 콜라이더
    public Collider2D Hurtbox { get; private set; }
    // 이 캐릭터의 공격 판정기. 공격 상태가 범위와 데미지를 넘겨 호출한다.
    public AttackHitChecker HitChecker { get; private set; }
    // Init을 마쳐 동작 중인지 여부. Release되면 다시 false가 된다.
    public bool IsInitialized { get; private set; }

    // 사망 애니메이션이 끝났음을 이미 알렸는지 여부. (상태가 끝난 뒤에도 매 프레임 확인하므로 한 번만 알린다)
    private bool _isDeathFinishedNotified;

    // 피격 판정 콜라이더가 속해야 하는 레이어 이름. 캐릭터 종류별로 정한다. (Player / Enemy)
    protected abstract string HurtboxLayerName { get; }
    // 이 캐릭터의 공격이 피해를 줄 피격 판정 레이어 이름. 상대 팀의 HurtboxLayerName이다. (Player → Enemy, Enemy → Player)
    protected abstract string TargetLayerName { get; }

    public void Init(BattleContext context)
    {
        if (IsInitialized)
        {
            Debug.LogWarning($"{name}: 이미 Init된 캐릭터입니다. 다시 Init하지 않습니다.", this);
            return;
        }

        // 설정 오류로 초기화하지 못하면 IsInitialized가 false로 남아 Update가 아무것도 하지 않는다.
        if (!OnInit(context))
        {
            return;
        }

        IsInitialized = true;
        OnInitCompleted();
    }

    // 피격 판정 콜라이더를 켜고 끈다. 꺼져 있는 동안에는 상대 공격의 판정 조회에 걸리지 않는다. (플레이어 대시 등)
    public void SetHurtboxEnabled(bool isEnabled)
    {
        if (Hurtbox != null)
        {
            Hurtbox.enabled = isEnabled;
        }
    }

    // 사망 상태가 사망 애니메이션이 끝난 것을 확인했을 때 호출한다. CharacterDeathFinishedEvent를 한 번만 발행한다.
    public void NotifyDeathFinished()
    {
        if (_isDeathFinishedNotified)
        {
            return;
        }

        _isDeathFinishedNotified = true;
        EventBus<CharacterDeathFinishedEvent>.Publish(new CharacterDeathFinishedEvent(gameObject));
    }

    public void Release()
    {
        if (!IsInitialized)
        {
            return;
        }

        IsInitialized = false;
        OnRelease();
    }

    // 참조 캐싱, 스탯 적용, 상태 생성, 설정 확인을 한다. 설정 오류로 진행할 수 없으면 false를 반환한다.
    // 실패하면 Release가 호출되지 않으므로 여기서는 이벤트를 구독하지 않는다. (구독은 OnInitCompleted에서 한다)
    // 자식 클래스에서 오버라이드할 때는 반드시 base.OnInit을 먼저 호출하고, false면 바로 false를 반환한다.
    protected virtual bool OnInit(BattleContext context)
    {
        Movement = GetComponent<CharacterMovement>();
        Animation = GetComponentInChildren<CharacterAnimation>();

        if (Animation == null)
        {
            Debug.LogError($"{name}: Visuals에 CharacterAnimation이 없습니다.", this);
            return false;
        }

        Animation.Initialize();

        BodyCollider = GetComponent<Collider2D>();
        Hurtbox = FindHurtbox();
        ValidateColliders();

        HitChecker = new AttackHitChecker(this, LayerMask.GetMask(TargetLayerName));
        return true;
    }

    // 모든 OnInit이 성공한 뒤 호출된다. 이벤트 구독, 상태기계 시작처럼 자식 클래스의 준비가 모두 끝나야 하는 일을 한다.
    // 자식 클래스에서 오버라이드할 때는 반드시 base.OnInitCompleted()를 먼저 호출한다.
    protected virtual void OnInitCompleted()
    {
    }

    // OnInit에서 구독한 이벤트를 해제한다. 자식 클래스에서 오버라이드할 때는 마지막에 base.OnRelease()를 호출한다.
    protected virtual void OnRelease()
    {
    }

    // 소환한 쪽이 Release하지 않고 파괴된 경우(씬 전환 등)에도 구독이 남지 않게 한다.
    private void OnDestroy()
    {
        Release();
    }

    // 이 캐릭터가 가진 공격들의 판정 범위를 그린다. 자식 클래스가 DrawHitboxGizmo로 자기 공격들을 그린다.
    // facingDirection: 그릴 기준 방향 (오른쪽 1, 왼쪽 -1)
    protected virtual void DrawHitboxGizmos(float facingDirection)
    {
    }

    // 판정 범위 하나를 붉은 선으로 그리고, 에디터에서는 박스 위에 이름을 표시한다.
    protected void DrawHitboxGizmo(HitboxShape hitbox, float facingDirection, string label)
    {
        Vector2 center = hitbox.GetCenter(transform.position, facingDirection);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(center, hitbox.Size);

#if UNITY_EDITOR
        UnityEditor.Handles.Label(center + new Vector2(-hitbox.Size.x * 0.5f, hitbox.Size.y * 0.5f), label);
#endif
    }

    // 디버그 표시를 켠 경우, 판정이 나간 범위를 일정 시간 동안 선택하지 않아도 그린다.
    private void OnDrawGizmos()
    {
        if (!_showHitRange || HitChecker == null || Time.time - HitChecker.LastHitTime > HitRangeDisplayTime)
        {
            return;
        }

        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawCube(HitChecker.LastHitCenter, HitChecker.LastHitSize);
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(HitChecker.LastHitCenter, HitChecker.LastHitSize);
    }

    // 선택했을 때 모든 공격의 판정 범위를 그린다. 플레이 중에는 지금 보는 방향, 편집 중에는 오른쪽 기준이다.
    private void OnDrawGizmosSelected()
    {
        float facingDirection = Application.isPlaying && Animation != null ? Animation.FacingDirection : 1f;
        DrawHitboxGizmos(facingDirection);
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

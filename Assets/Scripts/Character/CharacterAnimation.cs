using System;
using UnityEngine;

// 플레이어/적 공통 베이스. 상태가 요청한 애니메이션을 재생하고, 이동 방향에 따라 스프라이트를 반전한다.
// 공격 클립의 애니메이션 이벤트(OnAttackHitFrame)를 받아 AttackHitFrame 이벤트로 상태에 전달한다.
// 애니메이션 상태 이름(해시) 등 캐릭터 종류별 내용은 자식 클래스(PlayerAnimation, EnemyAnimation)에 둔다.
// 초기화는 Character가 Initialize()를 호출해서 한다.
[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public abstract class CharacterAnimation : MonoBehaviour
{
    private const float FacingThreshold = 0.01f;

    // 공격 애니메이션 상태들의 Speed Multiplier로 연결된 Animator 파라미터. 플레이어/적 컨트롤러 모두 같은 이름을 쓴다.
    private static readonly int AttackSpeedParameter = Animator.StringToHash("AttackSpeed");

    protected Animator animator;
    // 좌우 반전을 맡는 부모(Visuals). 이 오브젝트(Sprite)는 Visuals의 자식이어야 한다.
    private Transform _flipRoot;
    private float _baseScaleX;

    // 공격 클립의 타격 프레임에 도달했을 때 발생한다. 공격 상태가 진입할 때 구독하고 나갈 때 해제한다.
    public event Action AttackHitFrame;

    // 캐릭터가 보는 방향 (오른쪽 1, 왼쪽 -1)
    public float FacingDirection => _flipRoot.localScale.x < 0f ? -1f : 1f;

    public virtual void Initialize()
    {
        animator = GetComponent<Animator>();
        _flipRoot = transform.parent;

        if (_flipRoot == null || _flipRoot.GetComponent<Character>() != null)
        {
            Debug.LogError($"{name}: 좌우 반전용 부모(Visuals)가 없습니다. 캐릭터 루트 > Visuals > Sprite 구조여야 합니다.", this);
            _flipRoot = transform;
        }

        _baseScaleX = Mathf.Abs(_flipRoot.localScale.x);
    }

    // Animator에 해당 애니메이션 상태가 있는지 확인한다.
    public bool HasState(int stateHash)
    {
        return animator.HasState(0, stateHash);
    }

    // 같은 애니메이션이라도 처음부터 다시 재생한다.
    public void Play(int stateHash)
    {
        animator.Play(stateHash, 0, 0f);
    }

    // 해당 애니메이션이 재생 중이고 한 번 끝까지 재생되었는지 확인한다.
    public bool IsFinished(int stateHash)
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return stateInfo.shortNameHash == stateHash && stateInfo.normalizedTime >= 1f;
    }

    // 해당 애니메이션이 끝난 뒤 지난 시간. 아직 끝나지 않았다면 0을 반환한다.
    // 상태 로직이 Animator 갱신보다 먼저 실행되어 끝난 뒤 한 프레임 늦게 알게 되므로, 그 프레임만큼도 더해 보정한다.
    public float GetTimeSinceFinished(int stateHash)
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        if (stateInfo.shortNameHash != stateHash || stateInfo.normalizedTime < 1f)
        {
            return 0f;
        }

        return (stateInfo.normalizedTime - 1f) * stateInfo.length + Time.deltaTime;
    }

    // 해당 애니메이션의 진행도(1이 한 번 재생 완료). 아직 그 애니메이션으로 바뀌기 전이면 0을 반환한다.
    public float GetNormalizedTime(int stateHash)
    {
        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

        if (stateInfo.shortNameHash != stateHash)
        {
            return 0f;
        }

        return stateInfo.normalizedTime;
    }

    // 1이 기본 속도. 공격 애니메이션 상태들의 재생 속도 배율을 바꾼다.
    public void SetAttackSpeed(float attackSpeed)
    {
        animator.SetFloat(AttackSpeedParameter, attackSpeed);
    }

    // 원본 스프라이트가 오른쪽을 보고 있으므로 왼쪽일 때 부모(Visuals)의 스케일 x를 음수로 뒤집는다.
    // 스프라이트의 위치 보정값도 함께 뒤집히므로 몸통과 콜라이더가 어긋나지 않는다.
    // 방향 입력이 없으면 마지막 방향을 유지한다.
    public void UpdateFacing(float direction)
    {
        if (direction > FacingThreshold)
        {
            SetFlipRootScaleX(_baseScaleX);
        }
        else if (direction < -FacingThreshold)
        {
            SetFlipRootScaleX(-_baseScaleX);
        }
    }

    private void SetFlipRootScaleX(float scaleX)
    {
        Vector3 scale = _flipRoot.localScale;

        if (Mathf.Approximately(scale.x, scaleX))
        {
            return;
        }

        scale.x = scaleX;
        _flipRoot.localScale = scale;
    }

#if UNITY_EDITOR
    // 새 캐릭터를 만들 때 한 번 실행한다. 현재 스프라이트(그림 전체)의 가로 중심이 루트 콜라이더 중심에 오도록 Sprite의 x 위치를 맞춘다.
    // 칼 같은 무기도 그림에 포함되므로, 몸통과 어긋나 보이면 Sprite의 x를 직접 조금 조정한다.
    [ContextMenu("Align Sprite To Body")]
    private void AlignSpriteToBody()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        Character owner = GetComponentInParent<Character>();
        Transform flipRoot = transform.parent;

        if (spriteRenderer == null || spriteRenderer.sprite == null || owner == null || flipRoot == null || flipRoot == owner.transform)
        {
            Debug.LogError($"{name}: 정렬할 수 없습니다. 스프라이트와 캐릭터 루트 > Visuals > Sprite 구조를 확인하세요.", this);
            return;
        }

        Collider2D bodyCollider = owner.GetComponent<Collider2D>();

        if (bodyCollider == null)
        {
            Debug.LogError($"{name}: 루트에 Collider2D가 없어 정렬할 수 없습니다.", this);
            return;
        }

        // 투명한 여백을 뺀 실제 그림의 가로 중심 (스프라이트 로컬 단위)
        Vector2[] vertices = spriteRenderer.sprite.vertices;
        float minX = float.MaxValue;
        float maxX = float.MinValue;

        for (int i = 0; i < vertices.Length; i++)
        {
            minX = Mathf.Min(minX, vertices[i].x);
            maxX = Mathf.Max(maxX, vertices[i].x);
        }

        float artCenterX = (minX + maxX) * 0.5f;
        float bodyCenterWorldX = owner.transform.TransformPoint(bodyCollider.offset).x;
        float flipRootScaleX = Mathf.Abs(flipRoot.lossyScale.x);
        float desiredArtCenterLocalX = (bodyCenterWorldX - flipRoot.position.x) / flipRootScaleX;

        UnityEditor.Undo.RecordObject(transform, "Align Sprite To Body");
        Vector3 localPosition = transform.localPosition;
        localPosition.x = desiredArtCenterLocalX - artCenterX * transform.localScale.x;
        transform.localPosition = localPosition;
    }
#endif

    // 애니메이션 이벤트 수신용. 공격 클립의 타격 프레임에 이 함수 이름으로 이벤트를 추가한다.
    public void OnAttackHitFrame()
    {
        AttackHitFrame?.Invoke();
    }
}

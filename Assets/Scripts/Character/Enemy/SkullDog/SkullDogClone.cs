using MoreMountains.Feedbacks;
using UnityEngine;

// SkullDog의 분신. 패턴 게이지 기믹(폭주 런) 동안 화면 안 무작위 위치에 나타나 플레이어에게 대시하고 사라지기를 되풀이한다. (Enemy_GDD 8.9)
// 보스(Enemy)가 아니라 스킬 객체다. 체력·상태기계·피격 판정이 없어 플레이어의 공격은 맞지 않고, 분신의 몸에 닿은 플레이어만 피해를 입는다.
//  - 한 바퀴: 숨음(HideDuration) → 화면 테두리의 무작위 위치에 나타나 준비 자세(ReadyDuration, 나타난 순간의 플레이어 위치를 기억) → 그 자리로 일직선 대시 → 마무리 모션 → 숨음
//  - 나타나는 자리: 바닥에 발이 닿는 자리, 양쪽 벽에 몸이 붙는 자리, 위쪽 끝에 등이 닿는 자리 중 무작위다. (네 테두리의 길이에 비례해 고른다)
//    위쪽 끝은 화면 끝이 아니라 보스 패턴 게이지 바(UI) 아래 가장자리에서 Padding Top만큼 아래다. (SkullDog.FieldMax) 분신이 UI에 가려지지 않는다.
//  - 연출: 대시하는 동안 몸 뒤로 속도선(스컬독 돌진과 같은 pixelFX status/haste-lines)을 대시 방향으로 기울여 보여 주고, 대시를 시작할 때 스컬독과 같은 대시 소리를 낸다.
//  - 대시하는 동안에만 몸(Body Hitbox)에 닿은 플레이어가 피해를 입는다. 한 번의 대시에서 한 번만 판정한다. 준비 자세와 마무리에는 피해가 없다.
//  - 마무리 모션이 끝날 때마다 기믹이 아직 진행 중인지(SkullDog.IsGimmickActive) 확인한다. 끝났으면 다시 나타나지 않고 꺼진다.
//  - 플레이어가 죽으면 어느 단계에 있든 바로 꺼진다.
//  - 지형과 충돌하지 않는다. 나타나는 자리와 대시 목표를 벽·바닥·위쪽 UI 안쪽(SkullDog.FieldMin~FieldMax에서 Padding만큼 안쪽)으로 잘라 겹치지 않게 한다.
//    위쪽에서 나타나면 준비 자세의 등이 UI 아래 가장자리 바로 밑(틈 포함)에 오고, 위로 높이 뻗는 대시 그림의 머리는 대시를 시작한 직후 잠깐 UI에 가려질 수 있다.
// SkullDog가 전투 시작 때 하나 만들어 꺼 두고(Initialize), 기믹마다 Begin으로 켠다. 기믹이 끊기면 Dismiss로 바로 끄고, SkullDog가 죽으면 파괴한다.
// 그림은 Animator 없이 스프라이트를 코드로 바꾼다. (준비 1장, 대시·마무리 각 2장뿐이다) 분신 표시는 SpriteRenderer의 색(반투명 푸른색)이며 프리팹에서 바꾼다.
public class SkullDogClone : MonoBehaviour
{
    private const string PlayerLayerName = "Player";
    // 한 번의 판정에서 처리할 최대 콜라이더 수
    private const int MaxOverlapCount = 4;
    // 플레이어에게서 떨어진 자리를 찾을 때 무작위 위치를 다시 뽑는 최대 횟수
    private const int MaxSpawnTries = 8;
    // 남은 거리가 이 값 이하면 도착한 것으로 본다. (단위: 월드 거리)
    private const float ArriveDistance = 0.001f;

    private enum Phase
    {
        Hidden,
        Ready,
        Dash,
        End,
    }

    [Header("그림")]
    [Tooltip("좌우 반전을 맡는 자식(Visuals)입니다. 보는 방향에 따라 x 스케일의 부호만 바꿉니다.")]
    [SerializeField] private Transform _visuals;
    [Tooltip("분신 그림을 그리는 SpriteRenderer입니다. 분신 표시(반투명 푸른색)는 이 렌더러의 Color로 정합니다.")]
    [SerializeField] private SpriteRenderer _spriteRenderer;
    [Tooltip("준비 자세 그림입니다. (Massacre Sprite Sheet_6)")]
    [SerializeField] private Sprite _readySprite;
    [Tooltip("대시 그림입니다. 순서대로 한 번 재생하고 마지막 그림을 유지합니다. (Massacre Sprite Sheet_7, 8)")]
    [SerializeField] private Sprite[] _dashSprites;
    [Tooltip("대시 그림 한 장을 보여 주는 시간입니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _dashFrameTime = 0.05f;
    [Tooltip("대시 속도선 효과의 회전 기준입니다. Visuals/MirroredVFX/Dash/DashLines를 연결합니다. 대시하는 동안 대시 방향으로 기울입니다. 켜고 끄는 것은 아래 두 피드백이 맡습니다.\n비워 두면 기울이지 않습니다.")]
    [SerializeField] private Transform _dashLines;
    [Tooltip("대시를 시작할 때 속도선을 켜는 피드백입니다. Visuals/MirroredVFX/Dash/DashLines_Enter의 MMF_Player를 연결합니다.\n비워 두면 속도선이 나오지 않습니다.")]
    [SerializeField] private MMF_Player _dashLinesEnter;
    [Tooltip("대시가 끝나거나 분신이 꺼질 때 속도선을 끄는 피드백입니다. Visuals/MirroredVFX/Dash/DashLines_Exit의 MMF_Player를 연결합니다.")]
    [SerializeField] private MMF_Player _dashLinesExit;
    [Tooltip("대시 마무리 그림입니다. 순서대로 한 번 재생하고 끝나면 숨습니다. (Massacre Sprite Sheet_9, 10)")]
    [SerializeField] private Sprite[] _endSprites;
    [Tooltip("마무리 그림 한 장을 보여 주는 시간입니다. 장수 x 이 값이 마무리 시간입니다. (단위: 초)")]
    [SerializeField, Min(0.01f)] private float _endFrameTime = 0.1f;

    [Header("동작")]
    [Tooltip("대시를 끝내고 숨어 있는 시간입니다. 이 시간이 지나면 무작위 위치에 다시 나타납니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _hideDuration = 0.5f;
    [Tooltip("나타난 뒤 대시하기 전까지 준비 자세로 멈춰 있는 시간입니다. 나타난 순간의 플레이어 위치를 기억해 두고, 이 시간이 지나면 그 자리로 대시합니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _readyDuration = 0.5f;
    [Tooltip("기억해 둔 플레이어 위치로 일직선 대시하는 속도입니다. (단위: 월드 거리/초)")]
    [SerializeField, Min(0.1f)] private float _dashSpeed = 30f;
    [Tooltip("나타날 자리를 고를 때 플레이어에게서 최소한 떨어뜨리는 거리입니다. 플레이어 바로 위에 겹쳐 나타나지 않게 합니다. 자리가 없으면 무시됩니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _minSpawnDistance = 3f;

    [Header("범위 (나타나는 테두리와 벽·바닥·천장 사이 거리)")]
    [Tooltip("분신 중심을 양쪽 벽에서 띄우는 거리입니다. 벽 쪽 테두리에 나타날 때 그림이 벽에 붙는 값(그림 반폭)으로 둡니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _paddingX = 1.9f;
    [Tooltip("분신 중심을 바닥에서 띄우는 거리입니다. 바닥 테두리에 나타날 때 발이 바닥에 닿는 값(몸의 반높이)으로 둡니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _paddingBottom = 0.7f;
    [Tooltip("분신 중심을 위쪽 한계(보스 패턴 게이지 바 UI의 아래 가장자리)에서 아래로 띄우는 거리입니다. 위쪽 테두리에 나타날 때 준비 자세 그림의 등이 UI 아래에서 얼마나 떨어질지 정합니다. 그림 등까지의 거리 0.75에 UI와의 틈을 더한 값입니다. 클수록 UI에서 더 떨어집니다. 대시 그림은 이보다 위로 뻗어 대시를 시작한 직후 잠깐 UI에 가려질 수 있습니다. (단위: 월드 거리)")]
    [SerializeField, Min(0f)] private float _paddingTop = 0.95f;

    [Header("판정")]
    [Tooltip("대시하는 동안의 몸 충돌 판정 범위입니다. 이 범위에 닿은 플레이어가 피해를 입습니다. (분신 위치 기준, 오른쪽을 볼 때 기준)")]
    [SerializeField] private HitboxShape _bodyHitbox = new HitboxShape(Vector2.zero, new Vector2(3.6f, 1.4f));
    [Tooltip("분신의 대시에 닿은 플레이어가 입는 피해의 공격력 배율입니다. 피해량 = SkullDog 스탯의 공격력 x 이 값. 한 번의 대시에서 한 번만 맞습니다.")]
    [SerializeField, Min(0f)] private float _damageMultiplier = 1f;

    private readonly Collider2D[] _overlapResults = new Collider2D[MaxOverlapCount];
    private SkullDog _owner;
    private ContactFilter2D _playerFilter;
    private float _baseScaleX;

    private Phase _phase;
    // 지금 단계에 들어온 뒤 지난 시간
    private float _phaseTime;
    private Vector2 _aimPoint;
    private Vector2 _direction;
    // 보는 방향 (오른쪽 1, 왼쪽 -1)
    private float _facing = 1f;
    private float _remainingDistance;
    // 이번 대시에서 이미 플레이어에 닿았는지. 닿았으면 다시 판정하지 않는다.
    private bool _hasHit;

    // SkullDog가 분신을 만든 직후 한 번 호출한다. 주인을 기억하고 꺼 둔다.
    public void Initialize(SkullDog owner)
    {
        _owner = owner;
        _baseScaleX = Mathf.Abs(_visuals.localScale.x);

        _playerFilter = new ContactFilter2D();
        _playerFilter.SetLayerMask(LayerMask.GetMask(PlayerLayerName));
        // 플레이어의 피격 판정은 트리거이므로 트리거도 조회한다.
        _playerFilter.useTriggers = true;

        // 켜는 피드백은 초기화할 때 속도선을 꺼 둔다.
        if (_dashLinesEnter != null)
        {
            _dashLinesEnter.Initialization();
        }

        if (_dashLinesExit != null)
        {
            _dashLinesExit.Initialization();
        }

        gameObject.SetActive(false);
    }

    // 기믹이 시작될 때 SkullDog가 호출한다. 켜고 바로 무작위 위치에 나타난다.
    public void Begin()
    {
        gameObject.SetActive(true);
        Appear();
    }

    // 분신을 바로 거둔다. 기믹이 중간에 끊겼을 때 SkullDog가 호출하고, 기믹이 끝난 뒤 마지막 대시를 마쳤을 때 스스로 호출한다.
    public void Dismiss()
    {
        // 대시 도중에 꺼져도 다음에 켤 때 속도선이 남아 있지 않게 먼저 끈다. (꺼진 오브젝트의 피드백은 재생되지 않는다)
        if (gameObject.activeInHierarchy)
        {
            StopDashLines();
        }

        gameObject.SetActive(false);
    }

    private void Update()
    {
        // 주인이 없어졌거나 플레이어가 죽었으면 바로 거둔다.
        // (기믹이 끝난 뒤 마지막 대시를 하러 나온 분신은 SkullDog.CancelGimmick이 거두지 않으므로 여기서 거둔다)
        if (_owner == null || _owner.IsTargetDead)
        {
            Dismiss();
            return;
        }

        _phaseTime += Time.deltaTime;

        switch (_phase)
        {
            case Phase.Hidden:
                if (_phaseTime >= _hideDuration)
                {
                    Appear();
                }

                break;

            case Phase.Ready:
                if (_phaseTime >= _readyDuration)
                {
                    StartDash();
                }

                break;

            case Phase.Dash:
                UpdateDash();
                break;

            case Phase.End:
                UpdateEnd();
                break;
        }
    }

    // 무작위 위치에 나타나 준비 자세를 잡는다. 이 순간의 플레이어 위치를 대시 목표로 기억한다.
    private void Appear()
    {
        Vector2 position = PickSpawnPosition();
        transform.position = position;

        _aimPoint = GetAimPoint(position);
        SetFacing(_aimPoint.x - position.x);

        _spriteRenderer.sprite = _readySprite;
        _spriteRenderer.enabled = true;
        SetPhase(Phase.Ready);
    }

    private void StartDash()
    {
        Vector2 toAim = _aimPoint - (Vector2)transform.position;
        _remainingDistance = toAim.magnitude;
        _direction = _remainingDistance > ArriveDistance ? toAim / _remainingDistance : new Vector2(_facing, 0f);
        _hasHit = false;

        SetFacing(_direction.x);
        SetPhase(Phase.Dash);
        ShowFrame(_dashSprites, _dashFrameTime);
        PlayDashLines();
        _owner.PlayDashSound();
    }

    // 목표 지점까지 일정한 속도로 옮기며 몸 판정을 낸다. 도착하면 마무리로 넘어간다.
    private void UpdateDash()
    {
        float stepDistance = Mathf.Min(_dashSpeed * Time.deltaTime, _remainingDistance);
        transform.position += (Vector3)(_direction * stepDistance);
        _remainingDistance -= stepDistance;

        ShowFrame(_dashSprites, _dashFrameTime);
        HitBody();

        if (_remainingDistance <= ArriveDistance)
        {
            StopDashLines();
            SetPhase(Phase.End);
            ShowFrame(_endSprites, _endFrameTime);
        }
    }

    // 마무리 모션을 재생한다. 끝나면 기믹이 아직 진행 중인지 확인해, 진행 중이면 숨었다가 다시 나타나고 끝났으면 꺼진다.
    private void UpdateEnd()
    {
        // 마무리 그림이 비어 있으면 마무리 시간 없이 바로 끝낸다.
        float endDuration = _endSprites != null ? _endSprites.Length * _endFrameTime : 0f;

        if (_phaseTime < endDuration)
        {
            ShowFrame(_endSprites, _endFrameTime);
            return;
        }

        if (!_owner.IsGimmickActive)
        {
            Dismiss();
            return;
        }

        _spriteRenderer.enabled = false;
        SetPhase(Phase.Hidden);
    }

    // 몸에 닿은 플레이어에게 피해를 준다. 한 번의 대시에서 한 번만 판정한다. (플레이어가 무적이어도 닿았으면 판정한 것으로 본다)
    private void HitBody()
    {
        if (_hasHit)
        {
            return;
        }

        Vector2 center = _bodyHitbox.GetCenter(transform.position, _facing);
        int overlapCount = Physics2D.OverlapBox(center, _bodyHitbox.Size, 0f, _playerFilter, _overlapResults);

        for (int i = 0; i < overlapCount; i++)
        {
            IDamageable target = _overlapResults[i].GetComponentInParent<IDamageable>();

            if (target == null)
            {
                continue;
            }

            _hasHit = true;
            float damage = _owner.Stats.AttackPower * _damageMultiplier;
            target.TakeDamage(new DamageInfo(damage, _owner.gameObject, _overlapResults[i].ClosestPoint(center)));
            return;
        }
    }

    // 나타날 자리를 고른다. 화면 테두리(바닥·천장·양쪽 벽에 붙은 자리)의 무작위 위치이며,
    // 플레이어에게서 Min Spawn Distance 이상 떨어진 자리를 몇 번 다시 뽑아 찾는다. (못 찾으면 마지막에 뽑은 자리)
    private Vector2 PickSpawnPosition()
    {
        GetMoveRange(out Vector2 min, out Vector2 max);
        Vector2 position = PickEdgePoint(min, max);

        if (_owner.IsTargetDead)
        {
            return position;
        }

        Vector2 targetCenter = _owner.Target.BodyCollider.bounds.center;

        for (int i = 0; i < MaxSpawnTries && Vector2.Distance(position, targetCenter) < _minSpawnDistance; i++)
        {
            position = PickEdgePoint(min, max);
        }

        return position;
    }

    // 분신이 있을 수 있는 범위(min~max)의 네 테두리 위에서 무작위 점 하나를 고른다. 테두리마다 길이에 비례해 뽑힌다.
    // 아래 테두리는 바닥에 발이 닿는 자리, 위 테두리는 천장에 등이 닿는 자리, 양옆은 벽에 몸이 붙는 자리다.
    private static Vector2 PickEdgePoint(Vector2 min, Vector2 max)
    {
        float width = max.x - min.x;
        float height = max.y - min.y;
        float distance = Random.value * (width * 2f + height * 2f);

        if (distance < width)
        {
            return new Vector2(min.x + distance, min.y);
        }

        distance -= width;

        if (distance < width)
        {
            return new Vector2(min.x + distance, max.y);
        }

        distance -= width;

        if (distance < height)
        {
            return new Vector2(min.x, min.y + distance);
        }

        distance -= height;
        return new Vector2(max.x, min.y + Mathf.Min(distance, height));
    }

    // 대시가 향할 지점. 지금 플레이어의 가로 위치이고, 높이는 분신의 발이 플레이어의 발 높이에 오는 자리다. (본체의 대시와 같은 기준)
    // 벽·바닥·천장과 겹치지 않는 범위로 자른다. 플레이어가 없으면(죽음) 지금 자리다.
    private Vector2 GetAimPoint(Vector2 position)
    {
        if (_owner.IsTargetDead)
        {
            return position;
        }

        Bounds targetBounds = _owner.Target.BodyCollider.bounds;
        GetMoveRange(out Vector2 min, out Vector2 max);

        float aimX = Mathf.Clamp(targetBounds.center.x, min.x, max.x);
        float aimY = Mathf.Clamp(targetBounds.min.y + _paddingBottom, min.y, max.y);
        return new Vector2(aimX, aimY);
    }

    // 분신의 중심이 있을 수 있는 범위. SkullDog가 계산한 벽·바닥·천장 안쪽 면에서 Padding만큼 안쪽이다.
    private void GetMoveRange(out Vector2 min, out Vector2 max)
    {
        min = _owner.FieldMin + new Vector2(_paddingX, _paddingBottom);
        max = _owner.FieldMax - new Vector2(_paddingX, _paddingTop);

        // 범위가 뒤집힐 만큼 좁은 맵에서는 가운데 한 점으로 모은다.
        if (max.x < min.x)
        {
            min.x = max.x = (min.x + max.x) * 0.5f;
        }

        if (max.y < min.y)
        {
            min.y = max.y = (min.y + max.y) * 0.5f;
        }
    }

    // 속도선을 대시 방향으로 기울이고 켠다. (SkullDog.PlayDashLines와 같은 방식)
    // 속도선은 좌우 반전을 맡는 Visuals 아래에 있으므로 보는 쪽(앞)을 +x로 둔 각도로 기울이면, 왼쪽을 볼 때는 부모의 반전이 맞춰 준다.
    private void PlayDashLines()
    {
        if (_dashLines != null)
        {
            float angle = Mathf.Atan2(_direction.y, Mathf.Abs(_direction.x)) * Mathf.Rad2Deg;
            _dashLines.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        if (_dashLinesEnter != null)
        {
            _dashLinesEnter.PlayFeedbacks();
        }
    }

    private void StopDashLines()
    {
        if (_dashLinesExit != null)
        {
            _dashLinesExit.PlayFeedbacks();
        }
    }

    private void SetPhase(Phase phase)
    {
        _phase = phase;
        _phaseTime = 0f;
    }

    // 지금 단계의 지난 시간에 맞는 그림을 보여 준다. 마지막 그림에서 멈춘다.
    private void ShowFrame(Sprite[] sprites, float frameTime)
    {
        if (sprites == null || sprites.Length == 0)
        {
            return;
        }

        int index = Mathf.Min((int)(_phaseTime / frameTime), sprites.Length - 1);
        _spriteRenderer.sprite = sprites[index];
    }

    // 보는 방향을 정한다. 방향이 없으면(0에 가까우면) 지금 방향을 유지한다.
    private void SetFacing(float direction)
    {
        if (Mathf.Abs(direction) > 0.01f)
        {
            _facing = Mathf.Sign(direction);
        }

        Vector3 scale = _visuals.localScale;
        scale.x = _facing * _baseScaleX;
        _visuals.localScale = scale;
    }

#if UNITY_EDITOR
    // 선택했을 때 몸 충돌 판정 범위를 붉은 선으로 그린다.
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(_bodyHitbox.GetCenter(transform.position, _facing), _bodyHitbox.Size);
    }
#endif
}

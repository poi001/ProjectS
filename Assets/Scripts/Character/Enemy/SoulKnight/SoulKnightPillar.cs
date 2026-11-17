using System;
using DG.Tweening;
using UnityEngine;

// SoulKnight 2페이즈 기도 기믹의 기둥 하나. 바닥 아래에서 솟아올랐다가, 기믹이 끝나면 다시 바닥 아래로 내려간다. (Enemy_GDD 6.11)
// 이 오브젝트의 위치가 기둥 꼭대기(윗면)의 가운데다. 옆면은 Wall 레이어 콜라이더라 플레이어가 벽타기·벽 점프를 할 수 있고,
// 윗면은 Ground 레이어 콜라이더(자식 Top)라 플레이어가 올라설 수 있다. 꼭대기에는 목표물(자식 Target)이 있으며 뽑힌 기둥만 켠다.
// Kinematic Rigidbody2D를 물리 스텝마다 옮긴다. (DOTween, Fixed 갱신)
// 솟아오르기 시작할 때는 옆면과 윗면을 모두 Ghost 레이어로 둔다. Ghost는 플레이어(Character 레이어)와 충돌하지 않으므로,
// 기둥이 솟는 자리에 서 있던 플레이어가 기둥에 밀려 위로 튕겨 올라가거나 실려 올라가지 않고 기둥이 몸을 통과해 솟는다.
// 플레이어의 몸이 기둥이 솟는 자리(기둥 폭 × 바닥~꼭대기 높이)에서 완전히 벗어나면, 옆면은 Wall, 윗면은 Ground 레이어로 되돌려 단단해진다.
// 단단해진 뒤에는 다시 Ghost가 되지 않는다. 겹침이 풀리지 않으면 그 기둥은 계속 Ghost다. (바닥~꼭대기 전체를 보므로 솟는 도중에도 겹침으로 본다)
// 프리팹의 콜라이더·그림 크기와 Height 값은 서로 맞춰 둔다.
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class SoulKnightPillar : MonoBehaviour
{
    private const string GhostLayerName = "Ghost";
    // 숨어 있을 때 기둥 꼭대기가 바닥 표면보다 아래에 있는 깊이. 바닥 그림 위로 기둥 윗면이 비치지 않게 한다. (단위: 월드 거리)
    private const float HiddenDepth = 0.2f;
    // 플레이어가 기둥 자리에서 벗어났다고 볼 때 기둥 가장자리와 두는 여유. 단단해지는 순간 몸이 기둥에 끼어 밀려나지 않게 한다. (단위: 월드 거리)
    private const float OverlapMargin = 0.05f;

    [Tooltip("기둥의 높이입니다. 다 솟아올랐을 때 바닥에서 기둥 꼭대기까지의 거리이며, 프리팹의 콜라이더·그림 높이와 같아야 합니다. (단위: 월드 거리)")]
    [SerializeField, Min(0.1f)] private float _height = 6.5f;

    [Tooltip("기둥 꼭대기의 목표물입니다. 목표물이 놓이는 기둥으로 뽑혔을 때만 켜집니다.")]
    [SerializeField] private SoulKnightPillarTarget _target;

    [Tooltip("기둥 윗면 콜라이더입니다. 솟는 동안 이 콜라이더의 레이어를 Ghost로 바꿨다가 단단해질 때 원래 레이어(Ground)로 되돌립니다. 기둥 옆면은 이 오브젝트(루트)의 레이어를 같은 방식으로 바꿉니다.")]
    [SerializeField] private Collider2D _topCollider;

    private Rigidbody2D _rigidbody;
    private Tween _moveTween;
    private int _ghostLayer;
    private int _sideLayer;
    private int _topLayer;
    private float _halfWidth;
    // Ghost 상태인지 (솟기 시작할 때부터 플레이어가 자리에서 벗어날 때까지)
    private bool _isGhost;
    // 솟는 자리에서 벗어났는지 확인할 플레이어의 몸 콜라이더. 없으면 바로 단단해진다.
    private Collider2D _avoidBody;
    private float _centerX;
    private float _groundY;

    // 이 기둥의 목표물이 공격을 맞았을 때 발생한다.
    public event Action TargetHit;

    // 목표물(결석)이 켜져 있는지. 목표물로 뽑힌 기둥이 솟아 있고 아직 내려가기 전이면 true다.
    public bool HasActiveTarget => _target != null && _target.gameObject.activeInHierarchy;
    // 목표물(결석)의 월드 위치. 결석이 쏘는 번개가 시작되는 곳이다.
    public Vector2 TargetPosition => _target != null ? (Vector2)_target.transform.position : (Vector2)transform.position;
    // 목표물(결석) 오브젝트. 번개에 맞은 플레이어는 이 오브젝트의 반대쪽으로 밀려난다.
    public GameObject TargetObject => _target != null ? _target.gameObject : gameObject;

    // 기둥의 가로폭 (옆면 콜라이더 기준, 월드 거리). 프리팹 에셋에서도 읽을 수 있다. 기둥을 벽에 붙여 놓을 자리를 계산할 때 쓴다.
    public float Width
    {
        get
        {
            BoxCollider2D side = GetComponent<BoxCollider2D>();

            return side.size.x * Mathf.Abs(transform.lossyScale.x);
        }
    }

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();
        _ghostLayer = LayerMask.NameToLayer(GhostLayerName);
        _sideLayer = gameObject.layer;
        _topLayer = _topCollider != null ? _topCollider.gameObject.layer : _sideLayer;
        _halfWidth = Width * 0.5f;

        if (_ghostLayer < 0)
        {
            Debug.LogError($"SoulKnightPillar: '{GhostLayerName}' 레이어가 없어 플레이어와 겹친 채 솟을 때 튕겨 올라갈 수 있습니다.", this);
        }

        if (_target != null)
        {
            _target.Hit += OnTargetHit;
        }
    }

    // 솟는 도중 꺼지거나 풀로 돌아가도 단단한 상태는 다음 Rise에서 다시 정하므로, 여기서는 움직임만 멈춘다.
    private void OnDisable()
    {
        KillMove();
    }

    private void OnDestroy()
    {
        if (_target != null)
        {
            _target.Hit -= OnTargetHit;
        }
    }

    private void FixedUpdate()
    {
        if (!_isGhost || IsBodyInColumn())
        {
            return;
        }

        SetGhost(false);
    }

    // centerX 위치의 바닥(groundY) 아래에서 duration 동안 솟아오른다. hasTarget이면 꼭대기에 목표물을 켠다.
    // 솟기 시작할 때는 Ghost이며, avoidBody(플레이어의 몸 콜라이더)가 기둥이 솟는 자리에서 벗어나면 단단해진다. null이면 바로 단단해진다.
    public void Rise(float centerX, float groundY, bool hasTarget, float duration, Collider2D avoidBody)
    {
        KillMove();

        _centerX = centerX;
        _groundY = groundY;
        _avoidBody = avoidBody;
        SetGhost(true);

        Vector2 hiddenPosition = new Vector2(centerX, groundY - HiddenDepth);
        transform.position = hiddenPosition;
        _rigidbody.position = hiddenPosition;

        if (_target != null)
        {
            _target.ResetHit();
            _target.gameObject.SetActive(hasTarget);
        }

        // 천천히 출발해 천천히 멈추는 곡선(InOutSine)으로 올린다. 급하게 출발하거나 멈추면 기둥 위의 물체가 튕겨 오르기 쉽다.
        _moveTween = _rigidbody.DOMoveY(groundY + _height, duration)
            .SetEase(Ease.InOutSine)
            .SetUpdate(UpdateType.Fixed)
            .SetLink(gameObject);
    }

    // 지금 위치에서 duration 동안 바닥(groundY) 아래로 내려간다. 목표물은 바로 끈다.
    public void Sink(float groundY, float duration)
    {
        KillMove();

        if (_target != null)
        {
            _target.gameObject.SetActive(false);
        }

        _moveTween = _rigidbody.DOMoveY(groundY - HiddenDepth, duration)
            .SetEase(Ease.InCubic)
            .SetUpdate(UpdateType.Fixed)
            .SetLink(gameObject);
    }

    // 플레이어의 몸이 기둥이 솟는 자리(기둥 폭 × 바닥~꼭대기 높이, 여유 포함)와 겹쳐 있는지 확인한다.
    // 콜라이더 레이어와 상관없이 위치로만 판단한다. (기둥이 Ghost인 동안은 접촉 판정이 없으므로)
    private bool IsBodyInColumn()
    {
        if (_avoidBody == null)
        {
            return false;
        }

        Bounds body = _avoidBody.bounds;

        return body.max.x > _centerX - _halfWidth - OverlapMargin
            && body.min.x < _centerX + _halfWidth + OverlapMargin
            && body.max.y > _groundY - OverlapMargin
            && body.min.y < _groundY + _height + OverlapMargin;
    }

    // 옆면(루트)과 윗면의 레이어를 Ghost로 바꾸거나 원래 레이어(Wall, Ground)로 되돌린다.
    private void SetGhost(bool isGhost)
    {
        _isGhost = isGhost;

        if (_ghostLayer < 0)
        {
            return;
        }

        gameObject.layer = isGhost ? _ghostLayer : _sideLayer;

        if (_topCollider != null)
        {
            _topCollider.gameObject.layer = isGhost ? _ghostLayer : _topLayer;
        }
    }

    private void KillMove()
    {
        if (_moveTween != null)
        {
            _moveTween.Kill();
            _moveTween = null;
        }
    }

    private void OnTargetHit()
    {
        TargetHit?.Invoke();
    }
}

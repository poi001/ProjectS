using System;
using DG.Tweening;
using UnityEngine;

// SoulKnight 2페이즈 기도 기믹의 기둥 하나. 바닥 아래에서 솟아올랐다가, 기믹이 끝나면 다시 바닥 아래로 내려간다. (Enemy_GDD 6.11)
// 이 오브젝트의 위치가 기둥 꼭대기(윗면)의 가운데다. 옆면은 Wall 레이어 콜라이더라 플레이어가 벽타기·벽 점프를 할 수 있고,
// 윗면은 Ground 레이어 콜라이더(자식 Top)라 플레이어가 올라설 수 있다. 꼭대기에는 목표물(자식 Target)이 있으며 뽑힌 기둥만 켠다.
// Kinematic Rigidbody2D를 물리 스텝마다 옮기므로(DOTween, Fixed 갱신) 위에 서 있는 플레이어는 함께 밀려 올라간다.
// 프리팹의 콜라이더·그림 크기와 Height 값은 서로 맞춰 둔다.
[RequireComponent(typeof(Rigidbody2D))]
public class SoulKnightPillar : MonoBehaviour
{
    // 숨어 있을 때 기둥 꼭대기가 바닥 표면보다 아래에 있는 깊이. 바닥 그림 위로 기둥 윗면이 비치지 않게 한다. (단위: 월드 거리)
    private const float HiddenDepth = 0.2f;

    [Tooltip("기둥의 높이입니다. 다 솟아올랐을 때 바닥에서 기둥 꼭대기까지의 거리이며, 프리팹의 콜라이더·그림 높이와 같아야 합니다. (단위: 월드 거리)")]
    [SerializeField, Min(0.1f)] private float _height = 6.5f;

    [Tooltip("기둥 꼭대기의 목표물입니다. 목표물이 놓이는 기둥으로 뽑혔을 때만 켜집니다.")]
    [SerializeField] private SoulKnightPillarTarget _target;

    private Rigidbody2D _rigidbody;
    private Tween _moveTween;

    // 이 기둥의 목표물이 공격을 맞았을 때 발생한다.
    public event Action TargetHit;

    private void Awake()
    {
        _rigidbody = GetComponent<Rigidbody2D>();

        if (_target != null)
        {
            _target.Hit += OnTargetHit;
        }
    }

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

    // centerX 위치의 바닥(groundY) 아래에서 duration 동안 솟아오른다. hasTarget이면 꼭대기에 목표물을 켠다.
    public void Rise(float centerX, float groundY, bool hasTarget, float duration)
    {
        KillMove();

        Vector2 hiddenPosition = new Vector2(centerX, groundY - HiddenDepth);
        transform.position = hiddenPosition;
        _rigidbody.position = hiddenPosition;

        if (_target != null)
        {
            _target.ResetHit();
            _target.gameObject.SetActive(hasTarget);
        }

        // 천천히 출발해 천천히 멈추는 곡선(InOutSine)으로 올린다. 갑자기 출발하거나 급하게 멈추면
        // 기둥 위에 서 있던 플레이어가 튕겨 올라 화면 밖으로 나간다. 멈출 때의 감속이 플레이어에게 걸리는 중력보다 약해야 한다.
        // (높이 6.5, 1.2초면 최대 가속·감속 약 23으로 중력 29.4보다 약하다)
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

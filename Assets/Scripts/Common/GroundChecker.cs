using System;
using UnityEngine;

// checkPoint 위치의 OverlapBox로 Ground 레이어와의 접지 여부를 판정한다.
// 플레이어/적 공용 컴포넌트이므로 특정 캐릭터 전용 로직은 넣지 않는다.
// 이동 스크립트보다 먼저 갱신되도록 실행 순서를 앞당긴다.
[DefaultExecutionOrder(-100)]
public class GroundChecker : MonoBehaviour
{
    private const string GroundLayerName = "Ground";

    [SerializeField] private Transform _checkPoint;
    [SerializeField] private Vector2 _checkSize = new Vector2(0.5f, 0.1f);

    private int _groundLayerMask;

    public bool IsGrounded { get; private set; }

    public event Action OnLanded;
    public event Action OnLeftGround;

    private void Awake()
    {
        // 인스펙터 설정 실수로 다른 레이어에 반응하지 않도록 Ground 레이어로 고정한다.
        _groundLayerMask = LayerMask.GetMask(GroundLayerName);

        if (_groundLayerMask == 0)
        {
            Debug.LogError($"'{GroundLayerName}' 레이어가 존재하지 않습니다.", this);
        }

        if (_checkPoint == null)
        {
            Debug.LogError("GroundChecker의 checkPoint가 지정되지 않았습니다.", this);
            enabled = false;
        }
    }

    private void FixedUpdate()
    {
        bool wasGrounded = IsGrounded;
        IsGrounded = Physics2D.OverlapBox(_checkPoint.position, _checkSize, 0f, _groundLayerMask) != null;

        if (IsGrounded && !wasGrounded)
        {
            OnLanded?.Invoke();
        }
        else if (!IsGrounded && wasGrounded)
        {
            OnLeftGround?.Invoke();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (_checkPoint == null)
        {
            return;
        }

        Gizmos.color = IsGrounded ? Color.green : Color.red;
        Gizmos.DrawWireCube(_checkPoint.position, _checkSize);
    }
}

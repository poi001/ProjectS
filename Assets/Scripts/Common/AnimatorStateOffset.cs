using System;
using UnityEngine;

// 애니메이터의 현재 상태에 따라 이 오브젝트의 위치를 옮긴다. 몸에 붙은 연출(출혈 등)을 캐릭터의 자세(누움, 웅크림 등)에 맞출 때 쓴다.
// 상태 이름마다 옮길 양을 정해 두면, 그 상태인 동안 처음 위치에서 그만큼 옮기고, 목록에 없는 상태에서는 처음 위치로 돌아간다.
// 옮길 양은 월드 단위이며 x는 캐릭터가 바라보는 방향(앞)이 양수다. 좌우 반전은 부모(Visuals)가 맡으므로 왼쪽을 볼 때도 같은 값을 쓴다.
// 목록에 있는 자세로 들어갈 때는 Blend Time 동안 부드럽게 옮기고, 목록에 없는 상태(원래 자세)로 돌아갈 때는 Return Blend Time을 쓴다. (기본 0: 바로 돌아감)
// 켜질 때는 지금 상태의 위치로 바로 맞춘다.
public class AnimatorStateOffset : MonoBehaviour
{
    [Serializable]
    private class StateOffset
    {
        [Tooltip("애니메이터 상태 이름입니다. (예: Dead, Stun, Sliding) 컨트롤러의 상태 이름과 정확히 같아야 합니다.")]
        [SerializeField] private string _stateName;
        [Tooltip("이 상태인 동안 처음 위치에서 옮길 양입니다. (단위: 월드 거리) x는 캐릭터가 바라보는 방향이 양수, y는 위쪽이 양수입니다.")]
        [SerializeField] private Vector2 _offset;

        public string StateName => _stateName;
        public Vector2 Offset => _offset;
    }

    [Tooltip("상태를 읽을 애니메이터입니다. 캐릭터의 Visuals/Sprite에 있는 Animator를 연결합니다.")]
    [SerializeField] private Animator _animator;
    [Tooltip("상태별로 옮길 양입니다. 목록에 없는 상태에서는 처음 위치(옮긴 양 0)로 돌아갑니다.")]
    [SerializeField] private StateOffset[] _offsets = new StateOffset[0];
    [Tooltip("목록에 있는 상태로 들어갈 때 새 위치로 옮겨 가는 데 걸리는 시간입니다. 0이면 바로 옮깁니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _blendTime = 0.2f;
    [Tooltip("목록에 없는 상태(원래 자세, 예: Idle)로 돌아갈 때 처음 위치로 옮겨 가는 데 걸리는 시간입니다. 0이면 그 상태가 되자마자 바로 돌아갑니다. (단위: 초)")]
    [SerializeField, Min(0f)] private float _returnBlendTime;

    private int[] _stateHashes;
    private Vector3 _baseLocalPosition;
    // 지금 옮겨 둔 양과 옮겨 갈 목표 (월드 단위, x는 앞쪽 기준)
    private Vector2 _currentOffset;
    private Vector2 _targetOffset;
    // 목표가 바뀐 순간의 거리를 Blend Time으로 나눈 속도. 목표가 바뀔 때만 다시 구한다.
    private float _blendSpeed;

    private void Awake()
    {
        _baseLocalPosition = transform.localPosition;
        _stateHashes = new int[_offsets.Length];

        for (int i = 0; i < _offsets.Length; i++)
        {
            _stateHashes[i] = Animator.StringToHash(_offsets[i].StateName);
        }

        if (_animator == null)
        {
            Debug.LogError($"{name}: AnimatorStateOffset에 Animator가 연결되지 않아 위치를 옮기지 않습니다.", this);
        }
    }

    // 켜질 때는 지금 자세의 위치로 바로 맞춘다. (이전에 꺼지기 전 위치에서 미끄러져 오지 않게 한다)
    private void OnEnable()
    {
        _targetOffset = GetTargetOffset(out _);
        _currentOffset = _targetOffset;
        ApplyOffset();
    }

    private void OnDisable()
    {
        _currentOffset = Vector2.zero;
        transform.localPosition = _baseLocalPosition;
    }

    private void LateUpdate()
    {
        Vector2 target = GetTargetOffset(out bool isListedState);

        if (target != _targetOffset)
        {
            _targetOffset = target;
            float blendTime = isListedState ? _blendTime : _returnBlendTime;
            _blendSpeed = blendTime > 0f ? Vector2.Distance(_currentOffset, _targetOffset) / blendTime : float.PositiveInfinity;
        }

        if (_currentOffset == _targetOffset)
        {
            return;
        }

        _currentOffset = Vector2.MoveTowards(_currentOffset, _targetOffset, _blendSpeed * Time.deltaTime);
        ApplyOffset();
    }

    // 지금 상태의 옮길 양을 구한다. isListedState는 지금 상태가 목록에 있는지 여부다. (없으면 원래 위치, 옮길 양 0)
    private Vector2 GetTargetOffset(out bool isListedState)
    {
        isListedState = false;

        if (_animator == null || _stateHashes == null)
        {
            return Vector2.zero;
        }

        int stateHash = _animator.GetCurrentAnimatorStateInfo(0).shortNameHash;

        for (int i = 0; i < _stateHashes.Length; i++)
        {
            if (_stateHashes[i] == stateHash)
            {
                isListedState = true;
                return _offsets[i].Offset;
            }
        }

        return Vector2.zero;
    }

    // 월드 단위의 옮길 양을 부모 로컬 단위로 바꿔 적용한다. 부모의 x 스케일 부호(좌우 반전)는 무시해 앞쪽이 양수가 되게 한다.
    private void ApplyOffset()
    {
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        float scaleX = Mathf.Abs(parentScale.x) > 0f ? Mathf.Abs(parentScale.x) : 1f;
        float scaleY = Mathf.Abs(parentScale.y) > 0f ? Mathf.Abs(parentScale.y) : 1f;
        transform.localPosition = _baseLocalPosition + new Vector3(_currentOffset.x / scaleX, _currentOffset.y / scaleY, 0f);
    }
}

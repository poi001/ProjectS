using UnityEngine;

// 스프라이트 배열을 일정 간격으로 넘겨 재생하는 간단한 플립북. Animator 없이 짧은 스프라이트 FX(타격 이펙트 등)에 쓴다.
// 프레임에는 시트에서 슬라이스한 스프라이트를 재생 순서대로 넣는다.
// 재생 시간(초)을 지정하면 그 시간 동안 모든 프레임을 같은 간격으로 재생한다. (예: 1.0이면 1초 동안 재생)
// 한 시트에 여러 이펙트가 섞여 있어도 필요한 프레임만 골라 넣으면 되고, 낱장 스프라이트를 아틀라스로 묶은 경우도 같다.
// 활성화될 때마다 처음부터 재생한다. 반복하지 않으면 마지막 프레임이 끝난 뒤
// 풀에서 꺼낸 오브젝트는 풀로 반환하고, 그 외(씬에 직접 배치한 것 등)는 비활성화한다.
// 풀링할 때는 PooledObject의 반환 방식을 Manual로 두고 반환 시점은 이 컴포넌트에 맡긴다.
[RequireComponent(typeof(SpriteRenderer))]
public class Flipbook : MonoBehaviour
{
    [SerializeField] private Sprite[] _frames;
    // 모든 프레임을 한 번 재생하는 데 걸리는 시간(초)
    [SerializeField, Min(0.01f)] private float _duration = 0.35f;
    [SerializeField] private bool _loop;

    private SpriteRenderer _spriteRenderer;
    private PooledObject _pooledObject;
    private float _elapsedTime;
    private int _currentFrame;

    // 한 번 재생하는 데 걸리는 시간(초)
    public float Duration => _duration;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        TryGetComponent(out _pooledObject);
    }

    private void OnEnable()
    {
        Play();
    }

    // 첫 프레임부터 다시 재생한다.
    public void Play()
    {
        if (_frames == null || _frames.Length == 0)
        {
            Debug.LogError($"{name}: Flipbook에 프레임이 없습니다.", this);
            enabled = false;
            return;
        }

        enabled = true;
        _elapsedTime = 0f;
        _currentFrame = 0;
        _spriteRenderer.sprite = _frames[0];
    }

    private void Update()
    {
        _elapsedTime += Time.deltaTime;

        if (_elapsedTime >= _duration)
        {
            if (!_loop)
            {
                Finish();
                return;
            }

            // 경과 시간이 계속 커지지 않도록 한 바퀴 단위로 되돌린다.
            _elapsedTime %= _duration;
        }

        // 전체 재생 시간 대비 진행도로 프레임을 고른다. (부동소수점 오차로 범위를 넘지 않게 제한)
        int frame = Mathf.Min((int)(_elapsedTime / _duration * _frames.Length), _frames.Length - 1);

        if (frame != _currentFrame)
        {
            _currentFrame = frame;
            _spriteRenderer.sprite = _frames[frame];
        }
    }

    private void Finish()
    {
        if (_pooledObject != null && _pooledObject.IsSpawned && PoolManager.HasInstance)
        {
            PoolManager.Instance.Despawn(gameObject);
            return;
        }

        gameObject.SetActive(false);
    }
}

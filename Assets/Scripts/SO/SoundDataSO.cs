using UnityEngine;

// 사운드 하나(효과음 또는 배경음악)의 재생 데이터. SoundManager에 넘겨 재생한다.
// 클립을 여러 개 넣으면 재생할 때마다 그중 하나를 무작위로 고른다. (같은 소리가 반복될 때의 단조로움을 줄인다)
// 배경음악으로 쓸 때는 클립 하나, 피치 1~1로 두는 것을 권장한다.
[CreateAssetMenu(fileName = "SoundData", menuName = "SO/Sound Data")]
public class SoundDataSO : ScriptableObject
{
    [Tooltip("재생할 오디오 클립 목록입니다. 여러 개를 넣으면 재생할 때마다 그중 하나를 무작위로 고릅니다.")]
    [SerializeField] private AudioClip[] _clips;
    [Tooltip("재생 볼륨입니다. 0 = 무음, 1 = 클립 원래 크기입니다. 채널(Sfx, Bgm 등) 볼륨과는 별개로 곱해집니다.")]
    [SerializeField, Range(0f, 1f)] private float _volume = 1f;
    [Tooltip("재생할 때 무작위로 고르는 피치(재생 속도와 음높이 배율)의 최솟값입니다. 1 = 원래 높이입니다.\n최솟값과 최댓값이 같으면 항상 그 값으로 재생합니다.")]
    [SerializeField, Range(0.1f, 3f)] private float _minPitch = 1f;
    [Tooltip("재생할 때 무작위로 고르는 피치의 최댓값입니다. 최솟값보다 작게 둘 수 없습니다.\n효과음은 0.95~1.05 정도로 두면 반복감이 줄어듭니다.")]
    [SerializeField, Range(0.1f, 3f)] private float _maxPitch = 1f;
    [Tooltip("같은 사운드를 다시 재생하지 않는 최소 간격입니다. 이 시간 안에 다시 요청되면 무시합니다. (단위: 초, 실제 시간 기준)\n다단히트처럼 한 순간에 몰리는 소리가 겹쳐 터지는 것을 막습니다. 0 = 제한하지 않습니다.")]
    [SerializeField, Min(0f)] private float _minInterval;

    public float Volume => _volume;
    public float MinInterval => _minInterval;
    public bool HasClip => _clips != null && _clips.Length > 0;

    // 등록된 클립 중 하나를 무작위로 반환한다. 클립이 없으면 null.
    public AudioClip GetRandomClip()
    {
        if (!HasClip)
        {
            return null;
        }

        return _clips[Random.Range(0, _clips.Length)];
    }

    public float GetRandomPitch()
    {
        return Random.Range(_minPitch, _maxPitch);
    }

    private void OnValidate()
    {
        if (_maxPitch < _minPitch)
        {
            _maxPitch = _minPitch;
        }
    }
}

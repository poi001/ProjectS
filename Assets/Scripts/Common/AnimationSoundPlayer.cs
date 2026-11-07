using UnityEngine;

// 애니메이션 클립의 이벤트로 소리를 재생한다. Animator가 붙은 오브젝트에 함께 붙인다. (애니메이션 이벤트는 같은 오브젝트의 컴포넌트에만 전달된다.)
// 사용법: 애니메이션 창에서 소리를 낼 프레임에 이벤트를 추가하고, 함수는 PlaySound, 인자(Object)에는 SoundDataSO를 끌어다 놓는다.
// 소리의 종류와 타이밍을 클립에서 정하므로, 보스나 패턴이 늘어나도 코드를 추가하지 않는다.
public class AnimationSoundPlayer : MonoBehaviour
{
    // 애니메이션 이벤트 수신용. 이벤트의 함수 이름은 PlaySound, 인자는 SoundDataSO다.
    public void PlaySound(SoundDataSO soundData)
    {
        if (soundData == null || !SoundManager.HasInstance)
        {
            return;
        }

        SoundManager.Instance.PlaySfx(soundData);
    }
}

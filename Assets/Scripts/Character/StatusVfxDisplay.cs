using System.Collections.Generic;
using UnityEngine;

// 캐릭터 머리 위(상태 표시 위치)에 상태 VFX(기절, 감지, 버프 등)를 띄운다. 현재는 적(Enemy)이 사용한다.
// 캐릭터 프리팹 루트에 붙이고, 머리 위의 빈 자식 오브젝트(StatusVfxPoint)를 Anchor로 연결한다.
// Anchor는 좌우 반전되는 Visuals 밖(루트 바로 아래)에 둔다.
// VFX는 PoolManager로 꺼내며 캐릭터의 자식으로 붙이지 않는다. 대신 지속형 VFX는 매 프레임 Anchor 위치를 따라가게 한다.
// (캐릭터가 파괴될 때 자식 VFX가 함께 파괴되지 않고 풀로 돌아갈 수 있도록 하기 위함이며, VFX 크기도 캐릭터 스케일과 관계없이 프리팹 스케일 그대로다.)
// - 지속형(기절, 버프 등): Show(prefab)로 켜고 Hide(prefab)로 끈다. 같은 프리팹은 하나만 뜬다.
//   프리팹의 Flipbook은 반복(Loop)을 켜고, PooledObject 반환 방식은 Manual로 둔다.
// - 일회성(감지 등): Play(prefab). 뜬 자리에 머물며, 프리팹 쪽(반복하지 않는 Flipbook, PooledObject 자동 반환)이 스스로 풀로 돌아간다.
// 이 컴포넌트가 꺼지면(캐릭터 정리, 파괴 등) 켜져 있던 지속형 VFX를 모두 풀로 돌려보낸다.
// 여러 지속형 VFX가 동시에 뜨면 같은 위치에 겹친다. (나란히 배치가 필요해지면 그때 추가한다)
public class StatusVfxDisplay : MonoBehaviour
{
    [Tooltip("상태 VFX를 띄울 위치입니다. 캐릭터 루트의 자식으로 머리 위에 두며, 좌우 반전되는 Visuals 아래에는 두지 않습니다.\n비워 두면 캐릭터 루트 위치에 띄웁니다.")]
    [SerializeField] private Transform _anchor;

    // 지속형 VFX: 프리팹 → 떠 있는 인스턴스
    private readonly Dictionary<GameObject, GameObject> _activeVfx = new Dictionary<GameObject, GameObject>();

    public Transform Anchor => _anchor != null ? _anchor : transform;

    private void LateUpdate()
    {
        if (_activeVfx.Count == 0)
        {
            return;
        }

        Vector3 anchorPosition = Anchor.position;

        foreach (GameObject instance in _activeVfx.Values)
        {
            if (instance != null)
            {
                instance.transform.position = anchorPosition;
            }
        }
    }

    private void OnDisable()
    {
        HideAll();
    }

    // 지속형 VFX를 켠다. 이미 떠 있으면 그대로 둔다.
    public void Show(GameObject prefab)
    {
        if (prefab == null || _activeVfx.ContainsKey(prefab))
        {
            return;
        }

        GameObject instance = Spawn(prefab);

        if (instance != null)
        {
            _activeVfx.Add(prefab, instance);
        }
    }

    // 지속형 VFX를 끄고 풀로 돌려보낸다. 떠 있지 않으면 아무것도 하지 않는다.
    public void Hide(GameObject prefab)
    {
        if (prefab == null || !_activeVfx.TryGetValue(prefab, out GameObject instance))
        {
            return;
        }

        _activeVfx.Remove(prefab);
        Despawn(instance);
    }

    // 켜져 있는 지속형 VFX를 모두 끈다.
    public void HideAll()
    {
        foreach (GameObject instance in _activeVfx.Values)
        {
            Despawn(instance);
        }

        _activeVfx.Clear();
    }

    // 일회성 VFX를 한 번 띄운다. 반환은 프리팹 쪽이 스스로 한다.
    public void Play(GameObject prefab)
    {
        Spawn(prefab);
    }

    private GameObject Spawn(GameObject prefab)
    {
        if (prefab == null || !PoolManager.HasInstance)
        {
            return null;
        }

        return PoolManager.Instance.Spawn(prefab, Anchor.position, Quaternion.identity);
    }

    // 씬 전환 등으로 풀이나 인스턴스가 먼저 파괴된 경우는 건너뛴다.
    private static void Despawn(GameObject instance)
    {
        if (instance == null || !PoolManager.HasInstance)
        {
            return;
        }

        PoolManager.Instance.Despawn(instance);
    }
}

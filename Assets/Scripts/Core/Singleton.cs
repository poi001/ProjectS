using UnityEngine;

// MonoBehaviour 싱글톤 공통 베이스. 직접 상속하지 말고 PersistentSingleton 또는 SceneSingleton을 상속한다.
// 인스턴스를 자동 생성하지 않는다. 씬에 배치하거나 직접 생성한 오브젝트만 Instance로 등록된다.
// 자식 클래스는 Awake/OnDestroy 대신 OnAwake/OnDestroyed를 오버라이드한다. (Awake를 선언하면 CS0108 경고가 뜬다)
public abstract class SingletonBehaviour<T> : MonoBehaviour where T : SingletonBehaviour<T>
{
    public static T Instance { get; private set; }

    public static bool HasInstance => Instance != null;

    protected abstract bool IsPersistent { get; }

    protected void Awake()
    {
        if (Instance != null)
        {
            Debug.LogWarning($"[Singleton] {typeof(T).Name} 인스턴스가 이미 있어 중복 오브젝트를 삭제합니다: {gameObject.name}", this);
            Destroy(gameObject);
            return;
        }

        Instance = (T)this;

        if (IsPersistent)
        {
            // DontDestroyOnLoad는 루트 오브젝트에만 동작하므로 부모에서 분리한다.
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        OnAwake();
    }

    protected void OnDestroy()
    {
        if (Instance != this)
        {
            return;
        }

        OnDestroyed();
        Instance = null;
    }

    // 중복이 아닌 실제 인스턴스일 때만 호출된다.
    protected virtual void OnAwake()
    {
    }

    protected virtual void OnDestroyed()
    {
    }
}

// 씬이 바뀌어도 파괴되지 않는 싱글톤 (예: GameManager)
public abstract class PersistentSingleton<T> : SingletonBehaviour<T> where T : PersistentSingleton<T>
{
    protected sealed override bool IsPersistent => true;
}

// 해당 씬에서만 존재하고 씬이 바뀌면 함께 파괴되는 싱글톤 (예: BattleManager)
public abstract class SceneSingleton<T> : SingletonBehaviour<T> where T : SceneSingleton<T>
{
    protected sealed override bool IsPersistent => false;
}

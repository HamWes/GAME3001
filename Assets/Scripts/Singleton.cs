using UnityEngine;

public class Singleton<T> : MonoBehaviour where T : MonoBehaviour
{
    public static T Instance {  get; private set; }
    [SerializeField] private bool m_preserveOnLoad = true;

    protected virtual void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this as T;

        if (m_preserveOnLoad) DontDestroyOnLoad(gameObject);
    }
}

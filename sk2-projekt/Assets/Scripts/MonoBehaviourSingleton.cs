using UnityEngine;

public class MonoBehaviourSingleton<T> : MonoBehaviour where T : MonoBehaviour
{
    private static T _instance;
    private static bool _wasChecked;

    public static T Instance
    {
        get {
            if (_instance == null) {
                _instance = FindAnyObjectByType<T>();
            }
            if (!_wasChecked) {
                _wasChecked = true;
                if (_instance == null) {
                    Debug.LogWarning($"Couldn't find: '{typeof(T)}' on the scene.");
                }
            }
            return _instance;
        }
    }

    protected virtual void Awake()
    {
        if (_instance == null) {
            _instance = this as T;
        }
        if (_instance != this) {
            Destroy(this.gameObject);
            Debug.LogWarning($"Found multiple: '{typeof(T)}' on the scene.");
        }
    }
}

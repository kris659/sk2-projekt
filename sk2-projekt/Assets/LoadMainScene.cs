using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadMainScene : MonoBehaviour
{
    void Start()
    {
        SceneManager.LoadSceneAsync("MainScene", LoadSceneMode.Additive);
    }
}

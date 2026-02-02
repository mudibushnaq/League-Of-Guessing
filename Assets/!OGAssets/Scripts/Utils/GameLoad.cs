using UnityEngine.SceneManagement;
using UnityEngine;

public class GameLoad : MonoBehaviour {

    private void Start()
    {
        Invoke("LoadScene",2.5f);
    }

    void LoadScene()
    {
        SceneManager.LoadScene(1);
    }
}

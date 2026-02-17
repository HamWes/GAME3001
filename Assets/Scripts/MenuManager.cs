using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuManager : Singleton<MenuManager>
{
    [SerializeField] private bool canLoadScene = false;

    public void Click(string sceneName)
    {
        Debug.Log("Clicked a button.");
        LoadScene(sceneName);
    }

    public void LoadScene(string sceneName)
    {
        if (canLoadScene)
        {
            SceneManager.LoadScene(sceneName);
        }
    }
}

using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Exposes scene navigation and application exit actions to authored menu events.</summary>
public class MenuController : MonoBehaviour
{
    public void GoToLobby()
    {
        SceneManager.LoadScene(GameScenes.Lobby);
    }

    public void LoadScene(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }

    public void Quit()
    {
        Application.Quit();
    }
}

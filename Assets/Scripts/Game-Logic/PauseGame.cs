using UnityEngine;

/// <summary>Owns local pause-menu visibility and the session-disconnect action.</summary>
public class PauseGame : MonoBehaviour
{
    public static bool menuActive;
    private GameObject _pauseMenu;

    private void Start()
    {
        _pauseMenu = GameObject.Find("PauseMenu");
        menuActive = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) && !SettingsMenu.ConsumeEscape())
        {
            menuActive = !menuActive;
        }

        _pauseMenu.SetActive(menuActive);

    }

    public void Disconnect()
    {
        menuActive = false;
        if (CustomNetworkManager.Instance != null) CustomNetworkManager.Instance.Leave();
    }
}

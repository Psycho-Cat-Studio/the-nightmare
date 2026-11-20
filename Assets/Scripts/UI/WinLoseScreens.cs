using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Presents the match result and exposes replay or exit actions to the UI.</summary>
public class WinLoseScreens : MonoBehaviour
{
    private GameObject _win;
    private GameObject _lose;
    public static bool winLoseActive = false;

    private void Start()
    {
        _win = transform.GetChild(0).gameObject;
        _lose = transform.GetChild(1).gameObject;
    }

    public void ActivateScreen(bool isWin)
    {
        if(isWin)
            _win.SetActive(true);
        else
            _lose.SetActive(true);
        
        winLoseActive = true;
    }

    public void ReturnToMenu()
    {
        winLoseActive = false;
        if (SessionServices.Current != null) SessionServices.Current.LeaveToMainMenu();
        else SceneManager.LoadScene(GameScenes.MainMenu);
    }
}

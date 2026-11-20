using UnityEngine;

/// <summary>Advances the authored welcome/control screens and enables local lobby readiness input.</summary>
public class ButtonsControll : MonoBehaviour, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    public static bool screensOver = false;
    public void SkipWelcomeScreen()
    {
        transform.GetChild(0).gameObject.SetActive(false);
        transform.GetChild(1).gameObject.SetActive(true);
    }

    public void SkipControls()
    {
        transform.GetChild(1).gameObject.SetActive(false);
        screensOver = true;
        _game.Players.IsListeningForReady = true;
    }
}

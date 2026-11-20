using TMPro;
using UnityEngine;

/// <summary>Formats the session clock. Rendering or recreating the HUD cannot reset match time.</summary>
public class ClockManager : MonoBehaviour, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    [SerializeField] private TextMeshProUGUI _minutesTM;
    [SerializeField] private TextMeshProUGUI _secondsTM;

    void Start()
    {
        // Compatibility with experimental scenes; shipped scenes have explicit bindings.
        if (_minutesTM == null) _minutesTM = transform.GetChild(0).GetChild(0).GetComponent<TextMeshProUGUI>();
        if (_secondsTM == null) _secondsTM = transform.GetChild(0).GetChild(2).GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (_game == null) return;
        int totalSeconds = (int)_game.Clock.Remaining;
        _minutesTM.text = (totalSeconds / 60).ToString("D2");
        _secondsTM.text = (totalSeconds % 60).ToString("D2");
    }
}

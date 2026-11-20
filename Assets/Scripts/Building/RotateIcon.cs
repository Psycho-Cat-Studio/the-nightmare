using UnityEngine;

/// <summary>Aligns the minimap player icon with the locally controlled player heading.</summary>
public class RotateIcon : MonoBehaviour, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    private float _playerIconRotation = 0;
    private Quaternion _thisRotation;
    
    void Start() {
        _thisRotation = transform.rotation;
    }

    void Update() {
        if (_game?.Players.LocalPlayer != null) _playerIconRotation = _game.Players.LocalPlayer.transform.rotation.eulerAngles.y;
        transform.rotation = Quaternion.Euler(90, _thisRotation.y, -_playerIconRotation);
    }
}

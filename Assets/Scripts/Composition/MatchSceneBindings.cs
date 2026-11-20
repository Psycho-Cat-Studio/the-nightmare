using TMPro;
using UnityEngine;

/// <summary>Explicit scene composition. Assigned by the migration/editor, never searched per frame.</summary>
public sealed class MatchSceneBindings : MonoBehaviour
{
    public Transform RoomsRoot;
    public CapturePointsUI CaptureUI;
    public WinLoseScreens ResultScreens;
    public TextMeshProUGUI Instructions;
    public TextMeshProUGUI ReadyPlayers;
    public MusicManager Music;
    public BulletsHUD Bullets;
    public HealthBar Health;
    public ClipsManager Clips;
    public TextMeshProUGUI Grenades;
    public TextMeshProUGUI Interaction;
    public GameObject Crosshair;
    public Transform LobbySpawn;
    public Camera LobbyCamera;
    public TextMeshProUGUI[] PlaceableCounts;
}

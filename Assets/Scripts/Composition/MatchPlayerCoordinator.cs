using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

public enum PlayerControlMode { Building, Fighting }

/// <summary>Owns session-local player membership, local entry setup and player-mode presentation.</summary>
public sealed class MatchPlayerCoordinator
{
    private readonly GameManager _game;
    private const string PLAYER_ID_PREFIX = "Player ";
    private readonly Dictionary<string, PlayerManager> _players = new Dictionary<string, PlayerManager>();
    private readonly Dictionary<string, PlayerManager> _activePlayers = new Dictionary<string, PlayerManager>();
    private readonly List<int> arrowIndexList = new List<int> { 0, 1, 2, 3 };
    private PlayerManager _localPlayer;
    private bool _enteredCampaign;
    public bool HasEnteredCampaign => _enteredCampaign;
    public IReadOnlyDictionary<string, PlayerManager> Players => _players;
    public IReadOnlyDictionary<string, PlayerManager> ActivePlayers => _activePlayers;
    public bool IsListeningForReady { get; set; }
    public PlayerControlMode ControlMode { get; private set; } = PlayerControlMode.Fighting;
    public MatchPlayerCoordinator(GameManager game) { _game = game; }
    public PlayerManager LocalPlayer
    {
        get => _localPlayer;
        set
        {
            _localPlayer = value;
            var arrow = value != null ? value.GetComponentInChildren<Arrow>() : null;
            _game.Presenter.SetArrow(arrow);
            if (value != null && arrow == null) Debug.LogWarning("Local player has no Arrow component.");
        }
    }
    public bool AllPlayersDown()
    {
        if (_players.Count == 0) return false;
        foreach (var player in _players.Values) if (player != null && !player.IsDead) return false;
        return true;
    }
    public void SetControlMode(PlayerControlMode mode)
    {
        foreach (var player in _players.Values)
            if (mode == PlayerControlMode.Building) player.SetBuildingMode(); else player.SetActionMode();
        ControlMode = mode;
    }
    /// <summary>Registers a replicated player once and assigns its existing local color marker.</summary>
    public void RegisterPlayer(string netId, PlayerManager player)
    {
        string playerId = PLAYER_ID_PREFIX + netId;
        if (_players.TryGetValue(playerId, out var registered) && registered == player) return;
        _players.Add(playerId, player);
        _activePlayers.Add(playerId, player);
        player.transform.name = playerId;

        int tempIndex = Random.Range(0, arrowIndexList.Count);
        player.GetComponent<PlayerRigBindings>().ColorMarkers[0].SetActive(false);
        if (!player.isLocalPlayer && player.GetComponentInChildren<Outline>() != null)
        {
            Outline outline = player.GetComponentInChildren<Outline>();
            outline.enabled = true;
            switch (arrowIndexList[tempIndex])
            {
                case 0:
                    outline.OutlineColor = Color.green;
                    break;
                case 1:
                    outline.OutlineColor = Color.yellow;
                    break;
                case 2:
                    outline.OutlineColor = Color.blue;
                    break;
                case 3:
                    outline.OutlineColor = Color.red;
                    break;
            }
        }
        player.GetComponent<PlayerRigBindings>().ColorMarkers[arrowIndexList[tempIndex]].SetActive(true);
        arrowIndexList.RemoveAt(tempIndex); // was Remove(tempIndex): removed by value, so colours repeated
    }

    /// <summary>Releases player membership and color and updates authoritative lobby readiness.</summary>
    public void UnregisterPlayer(string playerId) {
        int index;
        var arrows = _players[playerId].GetComponent<PlayerRigBindings>().ColorMarkers;
        if (arrows[0].activeSelf)
            index = 0;
        else if (arrows[1].activeSelf)
            index = 1;
        else if (arrows[2].activeSelf) // checked child 1 twice before
            index = 2;
        else
            index = 3;

        if (!arrowIndexList.Contains(index)) arrowIndexList.Add(index);
        arrowIndexList.Sort();

        _players.Remove(playerId);
        if (_activePlayers.ContainsKey(playerId)) _activePlayers.Remove(playerId);
        if (_game.isServer && _game.TransportActive)
        {
            _game.PlayerDisconnected(playerId);
        }
    }

    public PlayerManager GetPlayer(string playerId)
    {
        if (!_players.ContainsKey(playerId)) return null;
        return _players[playerId];
    }

    public void DeactivatePlayer(string playerId)
    {
        _activePlayers.Remove(playerId);
    }

    public void ActivatePlayer(string playerId, PlayerManager player)
    {
        _activePlayers[playerId] = player;
    }


    /// <summary>Teleports the local player and restores the campaign's initial loadout exactly once per entry notification.</summary>
    public void EnterCampaign()
    {
        if (_enteredCampaign || LocalPlayer == null) return;
        _enteredCampaign = true;
        Vector3 pos = _game.World.CurrentRoom.CaptureAreas[0].transform.position;
        float randX = Random.Range(-2f, 2f);
        float randZ = Random.Range(-2f, 2f);
        Rigidbody rigidbody = LocalPlayer.GetComponent<Rigidbody>();
        rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
        rigidbody.isKinematic = true;
        Vector3 target = new Vector3(pos.x + randX, pos.y + 1f, pos.z + randZ);
        LocalPlayer.transform.position = target;
        rigidbody.position = target; // with interpolation the transform alone can be pulled back
        rigidbody.isKinematic = false;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _game.World.TurnOnGridRenders(false);
        PlayerEquipment playerEquipment = LocalPlayer.GetComponentInChildren<PlayerEquipment>();
        Object.Destroy(playerEquipment.Weapon1.gameObject);
        if (playerEquipment.Weapon2 != null) Object.Destroy(playerEquipment.Weapon2.gameObject);
        LocalPlayer.gameObject.GetComponent<PlayerSetup>().EquipWeapon();
        playerEquipment.CmdSetActiveWeapon(playerEquipment.Weapon1.Id); // everyone starts room 1 with the pistol

        //delete placeable
        var inventory = _localPlayer.GetComponent<PlacementController>().placeableCount;
        System.Array.Clear(inventory, 0, inventory.Length);
        _localPlayer.GetComponent<PlayerShoot>()._grenades = 0;
    }
}

using Mirror;
using UnityEngine;

/// <summary>Validates authoritative cell placement and delivers acceptance or rejection to the owning inventory.</summary>
[RequireComponent(typeof(PlacementController))]
public sealed class MirrorPlacement : NetworkBehaviour, IPlacementRequests, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    private PlacementController _placement;
    void Awake() { _placement = GetComponent<PlacementController>(); _placement.InitializePlacement(this); }
    public void Ready() => CmdReady();
    [Command] void CmdReady() { _game.ServerRegisterReady(transform.name); }
    public void Place(Vector3 position, Quaternion rotation, string cellId, int itemIndex) => CmdPlaceEntity(position, rotation, cellId, itemIndex);
    [TargetRpc] void TargetPlacementResult(NetworkConnectionToClient target, int index, bool accepted) { _placement.ResolvePlacement(index, accepted); }
    [Command]
    void CmdPlaceEntity(Vector3 pos, Quaternion rot, string tag, int placeableIndex)
    {
        string posAndTag = tag; // Stable authored cell ID, not a client-provided surface tag.
        if (!TryResolvePlacement(pos, placeableIndex, posAndTag, out var point))
        {
            TargetPlacementResult(connectionToClient, placeableIndex, false);
            return;
        }
        GameObject placeableObject = Instantiate(_placement.PlaceablePrefabs[placeableIndex], pos, rot);
        foreach (var behaviour in placeableObject.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour is IPlacedItem placed) placed.InitializeCell(posAndTag);
        NetworkServer.Spawn(placeableObject);
        point.Buildable = false;
        RpcPlaceEntity(posAndTag);
        TargetPlacementResult(connectionToClient, placeableIndex, true);
    }

    private bool TryResolvePlacement(Vector3 position, int itemIndex, string cellId, out GridPoint point)
    {
        point = null;
        bool validItem = itemIndex >= 0 && itemIndex < _placement.PlaceablePrefabs.Count;
        if (!validItem || !_placement.IsWithinPlacementRange(position)) return false;
        if (!_game.World.BuildingPoints.TryGetValue(cellId, out point) || !point.Buildable) return false;
        var offset = new Vector2(position.x - point.transform.position.x, position.z - point.transform.position.z);
        return offset.sqrMagnitude <= PlacementPositionToleranceSquared;
    }

    private const float PlacementPositionToleranceSquared = 0.01f;

    [ClientRpc]
    void RpcPlaceEntity(string posAndTag)
    {
        if (_game.World.BuildingPoints.TryGetValue(posAndTag, out var point)) point.Buildable = false;
    }

}

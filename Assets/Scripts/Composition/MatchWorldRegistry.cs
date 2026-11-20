using System.Collections.Generic;
using UnityEngine;
using Nightmare.Core.World;

/// <summary>Indexes authored world objects and owns the active room and scene-local enemy registry.</summary>
public sealed class MatchWorldRegistry
{
    private readonly List<GameObject> _rooms = new List<GameObject>();
    public IReadOnlyDictionary<string, IStageEnemy> Enemies => _enemies;
    public IReadOnlyDictionary<string, GridPoint> BuildingPoints => _buildingPoints;
    public IReadOnlyList<SpriteRenderer> SpriteRenderers => _spriteRenderers;
    public IReadOnlyList<GameObject> GridRenderers => _gridRenderers;
    public IReadOnlyList<CaptureArea> CaptureAreas => _captureAreas;
    public IReadOnlyList<ExPointBlink> EnemySpawnMarkers => _enemySpawnMarkers;
    public IReadOnlyDictionary<string, Transform> EnemySpawnPoints => _enemySpawnPoints;
    public IReadOnlyDictionary<string, CaptureArea> CurrentCaptureAreas => _currentCaptureAreas;
    public IReadOnlyList<GameObject> Rooms => _rooms;

    public Room CurrentRoom { get; private set; }
    public GameObject CurrentChest { get; private set; }
    private readonly Dictionary<string, CaptureArea> _currentCaptureAreas = new Dictionary<string, CaptureArea>();
    private readonly Dictionary<string, Transform> _enemySpawnPoints = new Dictionary<string, Transform>();
    private readonly List<ExPointBlink> _enemySpawnMarkers = new List<ExPointBlink>();
    private readonly List<CaptureArea> _captureAreas = new List<CaptureArea>();
    private readonly List<GameObject> _gridRenderers = new List<GameObject>();
    private readonly List<SpriteRenderer> _spriteRenderers = new List<SpriteRenderer>();
    private readonly List<CameraFacing> _billboards;
    private readonly Dictionary<string, GridPoint> _buildingPoints = new Dictionary<string, GridPoint>();
    private readonly Dictionary<string, IStageEnemy> _enemies = new Dictionary<string, IStageEnemy>();
    private readonly Dictionary<GridCellId, GridPoint> _locations = new Dictionary<GridCellId, GridPoint>();

    public MatchWorldRegistry(Transform rooms, List<CameraFacing> billboards)
    {
        _billboards = new List<CameraFacing>(billboards);
        foreach (Transform child in rooms)
        {
            var room = child.GetComponent<Room>();
            if (room == null) continue;
            room.Setup();
            _rooms.Add(room.gameObject);
            _gridRenderers.Add(room.GridRenderRoot);
            foreach (var point in room.GridPoints)
            {
                _buildingPoints.Add(point.Id, point);
                _locations.Add(CellLocation(point.transform.position, point.tag), point);
            }
            foreach (var area in room.CaptureAreas)
            {
                _captureAreas.Add(area);
                _spriteRenderers.Add(area.GetComponent<SpriteRenderer>());
                foreach (var facing in area.GetComponentsInChildren<CameraFacing>(true))
                    if (!_billboards.Contains(facing)) _billboards.Add(facing);
            }
        }
    }

    /// <summary>Replaces active-stage indices before the next preparation phase is presented.</summary>
    public void Activate(StageBinding binding, Camera camera)
    {
        CurrentRoom = binding.Room;
        CurrentChest = binding.Chest;
        _enemySpawnPoints.Clear(); _enemySpawnMarkers.Clear(); _currentCaptureAreas.Clear();
        foreach (var point in CurrentRoom.EnemySpawnPoint)
        {
            _enemySpawnPoints.Add(point.name, point.transform);
            if (point.transform.childCount == 0) continue;
            var marker = point.transform.GetChild(0);
            var facing = marker.GetComponent<CameraFacing>();
            if (facing != null)
            {
                if (!_billboards.Contains(facing)) _billboards.Add(facing);
                facing.cameraToLookAt = camera;
                facing.enabled = camera != null;
            }
            var blink = marker.GetComponent<ExPointBlink>();
            if (blink != null) _enemySpawnMarkers.Add(blink);
        }
        foreach (var area in CurrentRoom.CaptureAreas) _currentCaptureAreas.Add(area.Id, area);
        if (binding.EntryDoor != null) binding.EntryDoor.SetActive(false);
    }

    public bool ContainsCurrentArea(CaptureArea area) => area != null && _currentCaptureAreas.TryGetValue(area.Id, out var registered) && registered == area;
    public bool HasLostCurrentArea()
    {
        foreach (var area in _currentCaptureAreas.Values)
            if (area != null && (area.IsCaptured || area._progress <= 0f)) return true;
        return false;
    }
    private static GridCellId CellLocation(Vector3 position, string surface) => new GridCellId(surface,
        Mathf.RoundToInt(position.x / PlacementController.GridTileSize), Mathf.RoundToInt(position.z / PlacementController.GridTileSize));
    public GridPoint FindBuildingPoint(Vector3 position, string surface)
    { _locations.TryGetValue(CellLocation(position, surface), out var point); return point; }
    public void ReleaseBuildingPoint(string id)
    { if (id != null && _buildingPoints.TryGetValue(id, out var point)) point.Buildable = true; }
    /// <summary>Registers a transport-confirmed enemy identity within this scene.</summary>
    public void RegisterEnemy(string id, IStageEnemy enemy) { _enemies[id] = enemy; }
    /// <summary>Removes only the expected instance, so a stale destruction callback cannot remove a replacement.</summary>
    public void UnregisterEnemy(string id, IStageEnemy expected)
    { if (_enemies.TryGetValue(id, out var enemy) && ReferenceEquals(enemy, expected)) _enemies.Remove(id); }
    public void RegisterGridSprite(SpriteRenderer sprite) { _spriteRenderers.Add(sprite); }
    public void UnregisterGridSprite(SpriteRenderer sprite) { _spriteRenderers.Remove(sprite); }
    public IStageEnemy GetEnemy(string id) { _enemies.TryGetValue(id, out var enemy); return enemy; }
    public void TurnOnGridRenders(bool enabled)
    {
        foreach (var root in _gridRenderers) if (root != null) root.SetActive(enabled);
        foreach (var sprite in _spriteRenderers) if (sprite != null) sprite.enabled = enabled;
    }
    public void SetCameraForBillboards(Camera camera)
    {
        foreach (var facing in _billboards)
        {
            if (facing == null) continue;
            facing.cameraToLookAt = camera; facing.enabled = camera != null;
        }
    }
    /// <summary>Uses a snapshot because retirement can unregister enemies synchronously on the host.</summary>
    public void RetireSurvivors()
    {
        foreach (var enemy in new List<IStageEnemy>(_enemies.Values)) enemy?.RetireForStageCompletion();
    }
}

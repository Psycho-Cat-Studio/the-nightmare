using System.Collections.Generic;
using UnityEngine;

/// <summary>Authored scene references. Match rules and global registration belong to the composition root.</summary>
public class Room : MonoBehaviour
{
    [SerializeField] private int _width;
    [SerializeField] private int _height;
    [SerializeField] private int _areas;
    [SerializeField] private int _enemySpawnPointsCounter;
    [SerializeField] private bool _hasChest;
    [SerializeField] private Transform _gridRoot;
    [SerializeField] private Transform _captureRoot;
    [SerializeField] private Transform _spawnRoot;
    [SerializeField] private GameObject _renderRoot;
    private readonly List<GridPoint> _gridPoints = new List<GridPoint>();
    private readonly List<CaptureArea> _captureAreas = new List<CaptureArea>();
    private readonly List<GameObject> _enemySpawnPoints = new List<GameObject>();
    public int Width => _width;
    public int Height => _height;
    public int Areas => _areas;
    public int EnemySpawnPointsCounter => _enemySpawnPointsCounter;
    public IReadOnlyList<GridPoint> GridPoints => _gridPoints;
    public List<CaptureArea> CaptureAreas => _captureAreas;
    public List<GameObject> EnemySpawnPoint => _enemySpawnPoints;
    public GameObject GridRenderRoot => _renderRoot != null ? _renderRoot : transform.GetChild(0).gameObject;
    public bool HasChest { get => _hasChest; set => _hasChest = value; }

    /// <summary>Binds generated roots explicitly so optional chest children do not affect room hierarchy lookup.</summary>
    internal void BindGeneratedRoots(Transform points, Transform areas, Transform spawns, GameObject render)
    {
        _gridRoot = points;
        _captureRoot = areas;
        _spawnRoot = spawns;
        _renderRoot = render;
    }

    /// <summary>Rebuilds this room cached grid/capture/spawn collections from authored roots; repeated calls replace rather than duplicate entries.</summary>
    public void Setup()
    {
        _gridPoints.Clear(); _captureAreas.Clear(); _enemySpawnPoints.Clear();
        var grid = _gridRoot != null ? _gridRoot : transform.GetChild(1);
        var captures = _captureRoot != null ? _captureRoot : transform.GetChild(2);
        var spawns = _spawnRoot != null ? _spawnRoot : transform.GetChild(3);
        _gridPoints.AddRange(grid.GetComponentsInChildren<GridPoint>(true));
        _captureAreas.AddRange(captures.GetComponentsInChildren<CaptureArea>(true));
        foreach (Transform point in spawns) _enemySpawnPoints.Add(point.gameObject);
    }
}

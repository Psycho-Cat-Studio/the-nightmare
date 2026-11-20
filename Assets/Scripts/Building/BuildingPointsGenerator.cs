using Nightmare.Core.World;
using UnityEngine;

/// <summary>Legacy edit-mode authoring tool: rebuilds the GridCanvas room, grid, capture and spawn hierarchy in Awake.</summary>
/// <remarks>Used by fpsTest4, not by the shipped campaign. Generated roots are bound explicitly on Room.</remarks>
[ExecuteInEditMode]
public class BuildingPointsGenerator : MonoBehaviour
{
    private const string GridCanvasName = "GridCanvas";
    private const string RoomsResource = "Rooms";
    private const string GridVisualResource = "cross";
    private const string CaptureAreaResource = "CaptureArea";
    private const string ChestResource = "Chest";
    private GameObject _gridPointPrefab;
    private GameObject _areaPrefab;
    private GameObject _chestPrefab;

    private void Awake()
    {
        var canvas = GameObject.Find(GridCanvasName).GetComponent<Canvas>();
        ClearCanvas(canvas.transform);
        LoadPrefabs();
        var rooms = Instantiate(Resources.Load<GameObject>(RoomsResource), canvas.transform);
        rooms.name = RoomHierarchy.Rooms;
        for (int i = 0; i < rooms.transform.childCount; i++)
            GenerateRoom(rooms.transform.GetChild(i).GetComponent<Room>(), i);
    }

    private void LoadPrefabs()
    {
        _gridPointPrefab = Resources.Load<GameObject>(GridVisualResource);
        _areaPrefab = Resources.Load<GameObject>(CaptureAreaResource);
        _chestPrefab = Resources.Load<GameObject>(ChestResource);
    }

    private static void ClearCanvas(Transform canvas)
    {
        // DestroyImmediate changes child indices; increasing an index used to skip every second child.
        while (canvas.childCount > 0) DestroyImmediate(canvas.GetChild(0).gameObject);
    }

    private void GenerateRoom(Room room, int roomIndex)
    {
        var render = CreateContainer(RoomHierarchy.Render, room.transform);
        if (room.HasChest) Instantiate(_chestPrefab, room.transform.position, room.transform.rotation, room.transform);
        var points = CreateContainer(RoomHierarchy.Points, room.transform);
        GenerateGrid(room, points, render);
        var areas = CreateContainer(RoomHierarchy.Areas, room.transform);
        GenerateCaptureAreas(room, areas);
        var spawns = CreateContainer(RoomHierarchy.Spawns, room.transform);
        GenerateSpawnPoints(room, spawns, roomIndex);
        room.BindGeneratedRoots(points, areas, spawns, render.gameObject);
        room.Setup();
    }

    private void GenerateGrid(Room room, Transform points, Transform render)
    {
        foreach (var cell in BuildingGridLayout.Positions(room.transform.position.x, room.transform.position.z,
            room.Width, room.Height, PlacementController.GridTileSize))
        {
            var position = new Vector3(cell.X, points.position.y, cell.Z);
            var point = CreateContainer($"Point {cell.X} {cell.Z}", points).gameObject;
            point.transform.SetPositionAndRotation(position, Quaternion.identity);
            point.tag = room.tag;
            var gridPoint = point.AddComponent<GridPoint>();
            var visual = Instantiate(_gridPointPrefab, new Vector3(cell.X, render.position.y, cell.Z),
                Quaternion.Euler(90f, 0f, 0f), render);
            gridPoint.setSpriteRenderer(visual);
        }
    }

    private void GenerateCaptureAreas(Room room, Transform parent)
    {
        for (int i = 0; i < room.Areas; i++)
        {
            var area = Instantiate(_areaPrefab, parent);
            area.name = $"Area {i}";
        }
    }

    private static void GenerateSpawnPoints(Room room, Transform parent, int roomIndex)
    {
        for (int i = 0; i < room.EnemySpawnPointsCounter; i++)
            CreateContainer($"Spawn Point {i} Room{roomIndex}", parent);
    }

    private static Transform CreateContainer(string name, Transform parent)
    {
        var container = new GameObject(name).transform;
        container.SetParent(parent, false);
        return container;
    }
}

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Nightmare.Configuration;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

/// <summary>Owns one stage's spawn coroutine and marker lifecycle using authored enemy weights and existing cadence.</summary>
public sealed class EnemyWaveSpawner
{
    private readonly GameManager _game;
    private readonly IMatchTransport _transport;
    private readonly MatchWorldRegistry _world;
    private readonly MatchPlayerCoordinator _players;
    private readonly MatchSettings _matchSettings;
    private Coroutine _spawnEnemy;
    private StageDefinition _stage;
    private string _nameOfPreviousSpawn;
    public bool IsActive { get; private set; }
    public EnemyWaveSpawner(GameManager game, IMatchTransport transport, MatchWorldRegistry world,
        MatchPlayerCoordinator players, MatchSettings settings)
    { _game = game; _transport = transport; _world = world; _players = players; _matchSettings = settings; }
    /// <summary>Starts markers on every peer and the single spawn coroutine on authority, ignoring repeated starts.</summary>
    public void Start(StageDefinition stage, int stageIndex)
    {
        if (IsActive) return;
        _stage = stage;
        if (stageIndex > 0) _nameOfPreviousSpawn = null;
        IsActive = true;
        foreach (var marker in _world.EnemySpawnMarkers) marker.StartBlink();
        if (_transport.IsAuthority) _spawnEnemy = _game.StartCoroutine(SpawnEnemy());
    }
    /// <summary>Stops markers and cancels the owned wait/coroutine before another room becomes active.</summary>
    public void Stop()
    {
        if (!IsActive) return;
        IsActive = false;
        foreach (var marker in _world.EnemySpawnMarkers) if (marker != null) marker.StopBlink();
        if (_spawnEnemy != null) _game.StopCoroutine(_spawnEnemy);
        _spawnEnemy = null;
    }
    private IEnumerator SpawnEnemy()
    {
        while (true)
        {
            float upperTimeBound = _matchSettings.SpawnIntervalMaximum(_players.ActivePlayers.Count);
            yield return new WaitForSeconds(Random.Range(_matchSettings.SpawnIntervalMinimum, upperTimeBound));

            Transform spawnPoint = PickSpawnPoint();
            if (spawnPoint == null) continue;
            GameObject enemy = Object.Instantiate(_stage.SelectEnemy(Random.value), spawnPoint.position, spawnPoint.rotation);
            var target = spawnPoint.GetComponent<EnemySpawnTarget>();
            if (target != null) enemy.GetComponent<EnemyControllerServer>().SetInitialCaptureArea(target.InitialArea);
            MatchBindings.BindHierarchy(enemy, _game);
            _transport.Spawn(enemy);
        }
    }

    // Random spawn point, avoiding the previous one when there is a choice. The old version
    // crashed when the room had a single spawn point (it removed the only candidate).
    private Transform PickSpawnPoint()
    {
        List<Transform> candidates = new List<Transform>();
        foreach (KeyValuePair<string, Transform> entry in _world.EnemySpawnPoints)
            if (entry.Value != null && entry.Key != _nameOfPreviousSpawn) candidates.Add(entry.Value);
        if (candidates.Count == 0)
            foreach (Transform point in _world.EnemySpawnPoints.Values)
                if (point != null) candidates.Add(point);
        if (candidates.Count == 0) return null;

        Transform spawnPoint = candidates[Random.Range(0, candidates.Count)];
        _nameOfPreviousSpawn = spawnPoint.name;
        return spawnPoint;
    }


}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mirror;
using UnityEngine;

// Opt-in native client observer; -verifyPlacementInput additionally prepares keyboard/mouse QA.
public class PlacementNetworkVerification : MonoBehaviour
{
    [Serializable] class Item { public uint netId; public string type; public Vector3 position; }
    [Serializable] class Snapshot
    {
        public bool connected;
        public bool inputPrepared;
        public int players, occupied;
        public long utcTicks;
        public List<Item> items = new List<Item>();
    }
    float _next;
    float _prepareAfter;
    bool _inputPrepared;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (!Environment.GetCommandLineArgs().Contains("-verifyPlacementPeer")) return;
        var go = new GameObject("Placement peer observer");
        DontDestroyOnLoad(go);
        go.AddComponent<PlacementNetworkVerification>();
    }
    void Update()
    {
        if (!_inputPrepared && Environment.GetCommandLineArgs().Contains("-verifyPlacementInput") && GameManager.LocalPlayer != null)
        {
            if (_prepareAfter == 0) _prepareAfter = Time.unscaledTime + 1f;
            if (Time.unscaledTime >= _prepareAfter)
            {
                var player = GameManager.LocalPlayer;
                GameManager.Instance.TeleportToRoom1();
                player.GetComponent<Rigidbody>().isKinematic = true;
                player.GetComponent<PlacementController>().placeableCount = new[] { 3, 1, 3 };
                player.GetComponent<PlayerController>().enabled = false;
                player.GetComponent<PlayerMotor>().enabled = false;
                var camera = player.GetComponent<PlayerSetup>().ActionCamera;
                var point = GameManager.Instance.BuildingPoints.Values.Where(p => p.Buildable)
                    .OrderBy(p => (p.transform.position - player.transform.position).sqrMagnitude).First();
                camera.transform.LookAt(point.transform.position - Vector3.up * 0.08f);
                foreach (var screens in UnityEngine.Object.FindObjectsByType<ButtonsControll>()) { screens.SkipWelcomeScreen(); screens.SkipControls(); }
                _inputPrepared = true;
            }
        }
        if (Time.unscaledTime < _next) return;
        _next = Time.unscaledTime + 0.5f;
        var snapshot = new Snapshot { connected = NetworkClient.isConnected, inputPrepared = _inputPrepared, players = GameManager.Players.Count, utcTicks = DateTime.UtcNow.Ticks };
        if (GameManager.Instance != null)
            snapshot.occupied = GameManager.Instance.BuildingPoints.Values.Count(p => !p.Buildable);
        foreach (var identity in NetworkClient.spawned.Values)
        {
            if (identity == null) continue;
            string type = identity.GetComponent<Snares>() != null ? "Snares"
                : identity.GetComponent<TeddyBearServer>() != null ? "TeddyBear"
                : identity.GetComponentInChildren<Barrel>() != null ? "Barrel" : null;
            if (type != null) snapshot.items.Add(new Item { netId = identity.netId, type = type, position = identity.transform.position });
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/placement-network-client.json", JsonUtility.ToJson(snapshot, true));
    }
}

using UnityEngine;
using System.IO;
using System.Linq;
using System.Globalization;
var game=CustomNetworkManager.Instance.Game;

string dir = __RUN_DIR__;
string cellId = File.ReadAllText(Path.Combine(dir, "cell.txt"));
var cell = game.World.BuildingPoints[cellId];
float hitHealth = float.Parse(File.ReadAllText(Path.Combine(dir, "host-health.txt")), CultureInfo.InvariantCulture);
if (!cell.Buildable) throw new Exception("Server cell was not freed");
if (Object.FindObjectsByType<Snares>().Any(trap => trap.InitialPosAndTag == cellId))
    throw new Exception("Released trap remains on the server");
if (game.Players.LocalPlayer.GetComponent<PlacementController>().placeableCount[0] != 1)
    throw new Exception("Rejected placement spent host inventory");
if (game.Players.Players.Count != 2) throw new Exception("Peer disconnected during the test");
return new { passed = true, hitHealth, duplicateRejected = true, cellFreed = true, players = game.Players.Players.Count };

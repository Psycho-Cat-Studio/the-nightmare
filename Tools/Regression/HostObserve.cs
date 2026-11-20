using UnityEngine;using System.IO;using System.Linq;
var game=CustomNetworkManager.Instance.Game;
string dir=__RUN_DIR__;
if(File.Exists(Path.Combine(dir,"host-error.txt")))throw new Exception(File.ReadAllText(Path.Combine(dir,"host-error.txt")));
float hitHealth=float.Parse(File.ReadAllText(Path.Combine(dir,"host-health.txt")),System.Globalization.CultureInfo.InvariantCulture);
string cellId=File.ReadAllText(Path.Combine(dir,"cell.txt"));var cell=game.World.BuildingPoints[cellId];if(cell.Buildable)throw new Exception("Server cell reservation missing");
var host=game.Players.LocalPlayer;var position=cell.transform.position+Vector3.up;host.GetComponent<Rigidbody>().position=position;host.transform.position=position;host.GetComponent<PlacementController>().placeableCount[0]=1;host.GetComponent<MirrorPlacement>().Place(cell.transform.position,Quaternion.identity,cellId,0);await Task.Delay(100);if(host.GetComponent<PlacementController>().placeableCount[0]!=1)throw new Exception("Rejected placement spent inventory");
var trap=UnityEngine.Object.FindObjectsByType<Snares>().Single(p=>p.InitialPosAndTag==cellId);for(int i=0;i<3;i++)trap.RegisterCaughtEnemy();
await Task.Delay(100);if(!cell.Buildable)throw new Exception("Server cell was not freed");File.WriteAllText(Path.Combine(dir,"released-ready"),"ready");return new{passed=true,hitHealth,duplicateRejected=true,cellFreed=true,players=game.Players.Players.Count};

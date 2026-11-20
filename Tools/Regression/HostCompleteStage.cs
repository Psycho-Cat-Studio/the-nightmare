using UnityEngine;using UnityEditor;using UnityEngine.AI;using System.Linq;using System.IO;
var game=CustomNetworkManager.Instance.Game;
string dir=__RUN_DIR__;
game.ServerSetProgress(new Nightmare.Core.Match.MatchProgress(Nightmare.Core.Match.MatchPhase.Defend));
game.Spawner.Stop();game.Clock.IsRunning=false;
var area=game.World.CurrentRoom.CaptureAreas[0];
NavMeshHit point;if(!NavMesh.SamplePosition(area.transform.position+Vector3.right*2f,out point,5f,-1))throw new Exception("Missing cleanup test navmesh");
var stage=AssetDatabase.LoadAssetAtPath<Nightmare.Configuration.CampaignDefinition>("Assets/Configuration/Campaign.asset").Stages[0];
var enemies=new[]{"Skeleton","Ghost"}.Select((name,index)=>{
 var prefab=stage.Enemies.First(entry=>entry.Prefab.name.StartsWith(name)).Prefab;
 var instance=Object.Instantiate(prefab,point.position+Vector3.forward*index,Quaternion.identity);
 Mirror.NetworkServer.Spawn(instance);return instance.GetComponent<EnemyControllerServer>();
}).ToArray();
await Task.Delay(150);
File.WriteAllLines(Path.Combine(dir,"retired-enemies.txt"),enemies.Select(enemy=>enemy.name));
game.ServerSetProgress(new Nightmare.Core.Match.MatchProgress(Nightmare.Core.Match.MatchPhase.Prepare,1));
if(enemies.Any(enemy=>!enemy._isDying||enemy.GetComponentsInChildren<Collider>(true).Any(collider=>collider.enabled)))throw new Exception("Retirement did not disable authority combat/collisions immediately");
if(game.CanCapture(area))throw new Exception("Previous stage can still capture");
return new{passed=true,retired=enemies.Length,stage=game.StageIndex,phase=game.Phase.ToString()};

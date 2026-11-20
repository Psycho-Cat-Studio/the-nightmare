using UnityEngine;using Mirror;using System.Linq;using System.IO;using System.Collections.Generic;
var game=CustomNetworkManager.Instance.Game;string dir=__RUN_DIR__;
var prefab=game.Content.Placeables.First(item=>item.Prefab.GetComponent<Barrel>()!=null).Prefab;
var expected=new List<string>();var ids=new List<uint>();
string Key(Vector3 p)=>Mathf.RoundToInt(p.x*10)+":"+Mathf.RoundToInt(p.z*10);
for(int scenario=0;scenario<2;scenario++){
 var origin=new Vector3(-100f,20f,-100f-scenario*20f);float start=scenario==0?0f:2.2f;
 var barrels=new[]{start,start+2.2f,start+4.4f,start+8f}.Select(offset=>{
  var root=Object.Instantiate(prefab,origin+Vector3.right*offset,Quaternion.identity);NetworkServer.Spawn(root);var barrel=root.GetComponent<Barrel>();ids.Add(barrel.netId);return barrel;
 }).ToArray();
 foreach(var barrel in barrels.Take(3)){
  var sphere=barrel.GetComponentsInChildren<SphereCollider>(true).Single();
  if(Mathf.Abs(ExplosionFeedbackPool.WorldRadius(sphere)-2.5f)>.001f)throw new Exception("Unexpected default barrel radius");
  expected.Add(Key(sphere.transform.TransformPoint(sphere.center)));
 }
 if(scenario==0){barrels[0].Explode();barrels[0].Explode();}
 else{
  var root=Object.Instantiate(game.Players.LocalPlayer.GetComponent<PlayerShoot>().GrenadePrefab,origin,Quaternion.identity);var body=root.GetComponent<Rigidbody>();body.collisionDetectionMode=CollisionDetectionMode.Discrete;body.isKinematic=true;NetworkServer.Spawn(root);ids.Add(root.GetComponent<NetworkIdentity>().netId);
  var sphere=root.GetComponentsInChildren<SphereCollider>(true).Single();if(Mathf.Abs(ExplosionFeedbackPool.WorldRadius(sphere)-2.5f)>.001f)throw new Exception("Unexpected default grenade radius");expected.Add(Key(sphere.transform.TransformPoint(sphere.center)));
 }
 bool Exploded(Barrel barrel)=>(bool)typeof(Barrel).GetField("_exploded",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(barrel);
 await Wait.Until(()=>barrels.Take(3).All(Exploded),6000);
 if(Exploded(barrels[3]))throw new Exception("Out-of-range barrel joined the chain");
 NetworkServer.Destroy(barrels[3].gameObject);
}
await Wait.Until(()=>ids.All(id=>!NetworkServer.spawned.ContainsKey(id)),5000);
File.WriteAllLines(Path.Combine(dir,"chain-expected.txt"),expected);
File.WriteAllLines(Path.Combine(dir,"chain-netids.txt"),ids.Select(id=>id.ToString()));
File.WriteAllText(Path.Combine(dir,"chain-host-finished"),"ready");
return new{passed=true,scenarios=2,expectedEffects=expected.Count,allDespawned=true};

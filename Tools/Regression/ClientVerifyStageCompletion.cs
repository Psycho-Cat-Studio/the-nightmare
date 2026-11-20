using UnityEngine;using System.Reflection;using System.Linq;using System.IO;
string dir=__RUN_DIR__;
Type TypeOf(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
var names=File.ReadAllLines(Path.Combine(dir,"retired-enemies.txt"));
var type=TypeOf("EnemyControllerServer");
var enemies=Object.FindObjectsByType(type).Cast<Component>().Where(enemy=>names.Contains(enemy.name)).ToArray();
if(enemies.Length!=2)throw new Exception("Replica must observe both retirement effects before despawn");
foreach(var enemy in enemies){
 if(!(bool)type.GetField("_isDying").GetValue(enemy))throw new Exception("Replica retirement flag missing");
 if(enemy.GetComponentsInChildren<Collider>(true).Any(collider=>collider.enabled))throw new Exception("Replica colliders remain enabled");
 var hidden=(GameObject[])type.GetField("_deathHiddenObjects",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(enemy);
 if(hidden.Any(part=>part!=null&&part.activeSelf))throw new Exception("Replica dissolve preparation left auxiliary objects visible");
}
await Wait.Until(()=>names.All(name=>Object.FindObjectsByType(type).Cast<Component>().All(enemy=>enemy.name!=name)),8000);
return new{passed=true,retired=names.Length,despawned=true};

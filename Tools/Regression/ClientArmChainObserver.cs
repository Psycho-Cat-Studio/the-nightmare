using UnityEngine;using System.Reflection;using System.Linq;using System.IO;using System.Collections;using System.Collections.Generic;
string dir=__RUN_DIR__;
Type TypeOf(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
var effectType=TypeOf("ExplosionFeedback");
var seen=new HashSet<string>();var events=new Dictionary<string,int>();var radii=new Dictionary<string,float>();
string Key(Vector3 p)=>Mathf.RoundToInt(p.x*10)+":"+Mathf.RoundToInt(p.z*10);
IEnumerator Observe()
{
 float deadline=Time.realtimeSinceStartup+25f;
 while(Time.realtimeSinceStartup<deadline){
  foreach(var effect in Object.FindObjectsByType(effectType).Cast<Component>()){
   var center=(Vector3)effectType.GetProperty("Center").GetValue(effect);
   if(Mathf.Abs(center.y-20f)>.1f||center.x<-101f||center.x>-89f||(Mathf.Abs(center.z+100f)>.1f&&Mathf.Abs(center.z+120f)>.1f))continue;
   double started=(double)effectType.GetProperty("Started").GetValue(effect);
   string token=effect.GetEntityId()+":"+started.ToString("R",System.Globalization.CultureInfo.InvariantCulture);
   if(!seen.Add(token))continue;
   string key=Key(center);events.TryGetValue(key,out int count);events[key]=count+1;radii[key]=(float)effectType.GetProperty("Radius").GetValue(effect);
  }
  if(File.Exists(Path.Combine(dir,"chain-host-finished"))){
   var expected=File.ReadAllLines(Path.Combine(dir,"chain-expected.txt"));
   if(expected.All(key=>events.ContainsKey(key))){
    File.WriteAllLines(Path.Combine(dir,"chain-client-observed.txt"),events.Select(pair=>pair.Key+"\t"+pair.Value+"\t"+radii[pair.Key].ToString("R",System.Globalization.CultureInfo.InvariantCulture)));yield break;
   }
  }
  yield return null;
 }
 File.WriteAllLines(Path.Combine(dir,"chain-client-observed.txt"),new[]{"TIMEOUT"}.Concat(events.Select(pair=>pair.Key+"\t"+pair.Value)));
}
var owner=Object.FindObjectsByType(TypeOf("PlayerManager")).Cast<MonoBehaviour>().First();owner.StartCoroutine(Observe());
return new{armed=true};

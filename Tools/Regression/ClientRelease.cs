using UnityEngine;using System.IO;using System.Reflection;using System.Linq;
string dir=__RUN_DIR__;
Type TypeOf(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
var game=TypeOf("GameManager");var manager=Object.FindObjectsByType(game).Cast<Component>().First();var world=game.GetProperty("World").GetValue(manager);
var cells=(System.Collections.IDictionary)world.GetType().GetProperty("BuildingPoints").GetValue(world);
string cellId=File.ReadAllText(Path.Combine(dir,"cell.txt"));
await Wait.Until(()=>(bool)cells[cellId].GetType().GetProperty("Buildable").GetValue(cells[cellId]),5000);
return new{passed=true,cellFreed=true};

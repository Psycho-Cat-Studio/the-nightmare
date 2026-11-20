using UnityEngine;using System.Linq;using System.Reflection;
Type TypeOf(string name)=>AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(name)).First(t=>t!=null);
var game=TypeOf("GameManager"); object manager=null; object roster=null;
await Wait.Until(()=>{manager=Object.FindObjectsByType(game).Cast<Component>().FirstOrDefault(); if(manager==null)return false; roster=game.GetProperty("Players").GetValue(manager); return roster!=null && roster.GetType().GetProperty("LocalPlayer").GetValue(roster) is Component p && p!=null;},30000);
foreach(var screen in Object.FindObjectsByType(TypeOf("ButtonsControll")).Cast<Component>()){screen.GetType().GetMethod("SkipWelcomeScreen").Invoke(screen,null);screen.GetType().GetMethod("SkipControls").Invoke(screen,null);}
await Task.Delay(200);var player=(Component)roster.GetType().GetProperty("LocalPlayer").GetValue(roster);var placement=player.GetComponent(TypeOf("MirrorPlacement"));placement.GetType().GetMethod("Ready").Invoke(placement,null);return new{ready=true,name=player.name};

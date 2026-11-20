using UnityEngine;using System.IO;using System.Linq;using System.Collections;using System.Globalization;
var game=CustomNetworkManager.Instance.Game;
string dir=__RUN_DIR__;
IEnumerator ConfirmShotAndKill()
{
    float deadline=Time.realtimeSinceStartup+30f;
    while(!File.Exists(Path.Combine(dir,"shot-ready"))&&Time.realtimeSinceStartup<deadline)yield return null;
    // The client has compiled its observer and is ready before this event is sent.
    yield return new WaitForSeconds(.2f);
    try
    {
        var enemy=game.World.GetEnemy(File.ReadAllText(Path.Combine(dir,"enemy.txt"))) as EnemyControllerServer;
        if(enemy==null)throw new Exception("Enemy disappeared before confirmation");
        var field=typeof(EnemyControllerServer).GetField("_currentHealth",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        float hp=(float)field.GetValue(enemy);
        float expected=120f-game.Weapons[1].GetComponent<Weapon>().Definition.Damage*2f;
        if(Mathf.Abs(hp-expected)>.001f)throw new Exception("Client shot damage not confirmed: "+hp);
        File.WriteAllText(Path.Combine(dir,"host-health.txt"),hp.ToString(CultureInfo.InvariantCulture));
        var peer=game.Players.Players.Values.Single(p=>p!=game.Players.LocalPlayer);
        peer.TakeDamage(80);enemy.TakeDamage(1000);
    }
    catch(Exception error){File.WriteAllText(Path.Combine(dir,"host-error.txt"),error.ToString());}
}
game.StartCoroutine(ConfirmShotAndKill());return new{armed=true};

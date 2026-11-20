using UnityEngine;using UnityEngine.SceneManagement;
SceneManager.LoadScene("LobbyTests");await Task.Delay(500);
CustomNetworkManager.Instance.Host("Network regression",7799);
await Wait.Until(()=>CustomNetworkManager.Instance.Game != null && CustomNetworkManager.Instance.Game.Players.LocalPlayer != null);
var game=CustomNetworkManager.Instance.Game;
foreach(var screen in Object.FindObjectsByType<ButtonsControll>()){screen.SkipWelcomeScreen();screen.SkipControls();}
return new{ready=true,players=game.Players.Players.Count};

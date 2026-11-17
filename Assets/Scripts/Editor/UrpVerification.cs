using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;

// Small repeatable smoke test; run in Play mode, never modifies saved scene assets.
public static class UrpVerification
{
    static int step;
    static double next;
    static double deadline;
    [MenuItem("The Nightmare/URP/Smoke test host")]
    public static void StartHostTest()
    {
        if (!EditorApplication.isPlaying) throw new InvalidOperationException("Enter Play mode first.");
        step = 0;
        deadline = EditorApplication.timeSinceStartup + 120;
        next = 0;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
        SceneManager.LoadScene("LobbyTests");
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup > deadline)
        {
            EditorApplication.update -= Tick;
            File.WriteAllText("Logs/urp-smoke.txt", "FAIL: smoke test stopped or timed out at step " + step);
            return;
        }
        if (EditorApplication.timeSinceStartup < next) return;
        try
        {
            if (step == 0)
            {
                if (CustomNetworkManager.Instance == null) return;
                CustomNetworkManager.Instance.Host("URP verification", 7777);
                step = 1;
                next = EditorApplication.timeSinceStartup + 8;
            }
            else if (step == 1)
            {
                if (GameManager.Instance == null || GameManager.LocalPlayer == null) return;
                foreach (var screens in UnityEngine.Object.FindObjectsByType<ButtonsControll>())
                {
                    screens.SkipWelcomeScreen();
                    screens.SkipControls();
                }
                GameManager.Instance.ServerSetMatchState(GameManager.MatchState.Room1Prepare);
                step = 2;
                next = EditorApplication.timeSinceStartup + 5;
            }
            else if (step == 2)
            {
                ScreenCapture.CaptureScreenshot("Logs/urp-host.png");
                var cameras = Camera.allCameras;
                string report = "Host: " + NetworkServer.active + "\nClient: " + NetworkClient.isConnected
                    + "\nScene: " + SceneManager.GetActiveScene().name + "\nPlayer: " + GameManager.LocalPlayer.name;
                foreach (Camera camera in cameras)
                {
                    var data = camera.GetComponents<Component>().FirstOrDefault(c => c != null && c.GetType().Name == "UniversalAdditionalCameraData");
                    report += "\nCamera: " + camera.name + " target=" + camera.targetTexture + " depth=" + camera.depth;
                    if (data != null) report += " type=" + data.GetType().GetProperty("renderType").GetValue(data);
                }
                var bad = UnityEngine.Object.FindObjectsByType<Renderer>()
                    .SelectMany(r => r.sharedMaterials).Where(m => m != null && (m.shader == null || m.shader.name == "Hidden/InternalErrorShader" || !m.shader.isSupported));
                report += "\nUnsupported visible materials: " + string.Join(",", bad.Select(m => m.name).Distinct());
                File.WriteAllText("Logs/urp-smoke.txt", report);
                GameManager.Instance.ServerSetMatchState(GameManager.MatchState.Room1Fight);
                step = 3;
                next = EditorApplication.timeSinceStartup + 12;
            }
            else if (step == 3)
            {
                ScreenCapture.CaptureScreenshot("Logs/urp-fight.png");
                File.AppendAllText("Logs/urp-smoke.txt", "\nFight phase: " + GameManager.Instance.CurrentMachState
                    + "\nEnemies: " + UnityEngine.Object.FindObjectsByType<EnemyControllerServer>().Length);
                EditorApplication.update -= Tick;
                Debug.Log("[URP] Host smoke test completed. See Logs/urp-smoke.txt and screenshots.");
            }
        }
        catch (Exception e)
        {
            EditorApplication.update -= Tick;
            File.WriteAllText("Logs/urp-smoke.txt", "FAIL: " + e);
            Debug.LogException(e);
        }
    }
}

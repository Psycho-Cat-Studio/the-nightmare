using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

// Two separate player launches prove disk persistence, rather than reapplying in-memory prefs.
/// <summary>Runs opt-in two-launch checks of display and graphics preference persistence, restoring captured preferences.</summary>
public class DisplayVerification : MonoBehaviour
{
    /// <summary>Stores one preference value and its original presence for display-test restoration.</summary>
    [Serializable] class Entry { public string key; public bool present; public bool floating; public int integer; public float number; }
    /// <summary>Serializes the preferences that must survive the two display-verification launches.</summary>
    [Serializable] class Backup { public List<Entry> entries = new List<Entry>(); }
    const string BackupPath = "Logs/display-preferences-backup.json";
    static readonly string[] Flags = { "bloom", "vignette", "autoExposure", "depthOfField", "motionBlur", "colorGrading", "chromatic", "dithering", "ambientOcclusion", "aa" };
    bool _write;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        var args = Environment.GetCommandLineArgs();
        if (!args.Contains("-verifyDisplayWrite") && !args.Contains("-verifyDisplayRead")) return;
        var go = new GameObject("Display persistence verification");
        DontDestroyOnLoad(go);
        go.AddComponent<DisplayVerification>()._write = args.Contains("-verifyDisplayWrite");
    }
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static void Capture()
    {
        Require(!File.Exists(BackupPath), "Previous preference backup still exists; run -verifyDisplayRead to restore it first.");
        var backup = new Backup();
        foreach (string key in Flags.Select(k => "graphics." + k).Concat(new[] { GraphicsPreferenceKeys.Exposure, "settings.quality", "settings.maxFps", GraphicsPreferenceKeys.DisplayWidth, GraphicsPreferenceKeys.DisplayHeight, GraphicsPreferenceKeys.DisplayMode, GraphicsPreferenceKeys.RenderWidth, GraphicsPreferenceKeys.RenderHeight }))
            backup.entries.Add(new Entry { key = key, present = PlayerPrefs.HasKey(key), floating = key == GraphicsPreferenceKeys.Exposure, integer = PlayerPrefs.GetInt(key), number = PlayerPrefs.GetFloat(key) });
        Directory.CreateDirectory("Logs");
        File.WriteAllText(BackupPath, JsonUtility.ToJson(backup, true));
    }
    static void Restore()
    {
        foreach (var entry in JsonUtility.FromJson<Backup>(File.ReadAllText(BackupPath)).entries)
        {
            if (!entry.present) PlayerPrefs.DeleteKey(entry.key);
            else if (entry.floating) PlayerPrefs.SetFloat(entry.key, entry.number);
            else PlayerPrefs.SetInt(entry.key, entry.integer);
        }
        PlayerPrefs.Save();
        File.Delete(BackupPath);
    }
    IEnumerator Start()
    {
        var work = Run();
        string error = null;
        while (true)
        {
            bool next = false;
            object value = null;
            try { next = work.MoveNext(); if (next) value = work.Current; }
            catch (Exception exception) { error = exception.ToString(); }
            if (error != null || !next) break;
            yield return value;
        }
        if ((!_write || error != null) && File.Exists(BackupPath)) Restore();
        string result = error == null ? "PASS" : error;
        Directory.CreateDirectory("Logs");
        File.WriteAllText(_write ? "Logs/display-write.txt" : "Logs/display-read.txt", result);
        Debug.Log("[DisplayVerification] " + result);
        Application.Quit(error == null ? 0 : 1);
    }
    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(2);
        if (_write)
        {
            Capture();
            for (int i = 0; i < Flags.Length; i++) PlayerPrefs.SetInt("graphics." + Flags[i], i >= 3 ? 1 : 0);
            GraphicsPreferences.ManualExposure = -0.3f;
            GameSettings.QualityLevel = 2;
            GameSettings.MaxFps = 60;
            GraphicsPreferences.SetRenderScale(0.625f);
            Screen.SetResolution(Screen.currentResolution.width, Screen.currentResolution.height, FullScreenMode.FullScreenWindow);
            yield return new WaitForSecondsRealtime(2);
            GraphicsPreferences.SaveDisplay(Screen.width, Screen.height, Screen.fullScreenMode);
            GameSettings.Save();
        }
        else
        {
            Require(File.Exists(BackupPath), "Run -verifyDisplayWrite first.");
            Require(Screen.fullScreenMode == FullScreenMode.FullScreenWindow, "Borderless mode was not restored on startup.");
            Require(Mathf.Abs(GraphicsPreferences.RenderScale - 0.625f) < 0.002f, "Render resolution was not restored on startup.");
            Require(Mathf.Abs((float)UrpAccess.Get(QualitySettings.renderPipeline, "renderScale") - GraphicsPreferences.RenderScale) < 0.001f, "Runtime pipeline lost render resolution.");
            Require(QualitySettings.GetQualityLevel() == 2 && Application.targetFrameRate == 60 && QualitySettings.vSyncCount == 0, "Quality/FPS was not restored.");
            Require(!GraphicsPreferences.Bloom && !GraphicsPreferences.Vignette && !GraphicsPreferences.AutoExposure && GraphicsPreferences.DepthOfField && GraphicsPreferences.MotionBlur && GraphicsPreferences.Tonemapping && GraphicsPreferences.ChromaticAberration && GraphicsPreferences.Dithering && GraphicsPreferences.AmbientOcclusion && GraphicsPreferences.AntiAliasing == 1, "Effects were not restored.");
            Require(Mathf.Abs(GraphicsPreferences.ManualExposure + 0.3f) < 0.001f, "Manual exposure was not restored.");
            var menu = UnityEngine.Object.FindAnyObjectByType<SettingsMenu>();
            menu.OpenWindow();
            var apply = typeof(SettingsMenu).GetMethod("ApplyDisplay", BindingFlags.Instance | BindingFlags.NonPublic);
            apply.Invoke(menu, new object[] { Screen.currentResolution.width / 2, Screen.currentResolution.height / 2, true });
            yield return new WaitForSecondsRealtime(1);
            Require(Mathf.Abs(GraphicsPreferences.RenderScale - 0.5f) < 0.001f, "Borderless trial did not change render resolution.");
            menu.CancelDisplay();
            yield return new WaitForSecondsRealtime(1);
            Require(Mathf.Abs(GraphicsPreferences.RenderScale - 0.625f) < 0.002f, "Revert did not restore render resolution.");
            menu.CloseWindow();
            GameSettings.QualityLevel = 5;
            yield return new WaitForSecondsRealtime(1);
            Require(Mathf.Abs((float)UrpAccess.Get(QualitySettings.renderPipeline, "renderScale") - 0.625f) < 0.002f, "Quality change lost render resolution.");
        }
    }
}

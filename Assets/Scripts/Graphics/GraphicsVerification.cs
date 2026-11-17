using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Opt-in integration test: launch a player with -verifyGraphics. Never runs in normal gameplay.
public class GraphicsVerification : MonoBehaviour
{
    [Serializable] class Report { public bool passed; public string error; public List<string> checks = new List<string>(); }
    class Preference { public string key; public bool present; public bool floating; public int integer; public float number; }
    readonly List<Preference> _preferences = new List<Preference>();
    readonly Report _report = new Report();
    int _quality, _width, _height;
    FullScreenMode _mode;
    bool _pause;
    float _renderScale;
    GameObject _probe;
    Material _probeMaterial;
    readonly List<Behaviour> _disabled = new List<Behaviour>();
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoRun()
    {
        if (!Environment.GetCommandLineArgs().Contains("-verifyGraphics")) return;
        var go = new GameObject("Graphics verification");
        DontDestroyOnLoad(go);
        go.AddComponent<GraphicsVerification>();
    }
    static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    void Capture()
    {
        _quality = QualitySettings.GetQualityLevel(); _width = Screen.width; _height = Screen.height;
        _mode = Screen.fullScreenMode; _pause = PauseGame.menuActive;
        _renderScale = GraphicsPreferences.RenderScale;
        string[] ints = { "bloom", "vignette", "autoExposure", "depthOfField", "motionBlur", "colorGrading", "chromatic", "dithering", "ambientOcclusion", "aa" };
        foreach (string key in ints) SaveKey("graphics." + key, false);
        foreach (string key in new[] { "exposure" }) SaveKey("graphics." + key, true);
        foreach (string key in new[] { "display.width", "display.height", "display.mode", "display.renderWidth", "display.renderHeight", "settings.quality" }) SaveKey(key, false);
    }
    void SaveKey(string key, bool floating)
    {
        _preferences.Add(new Preference { key = key, floating = floating, present = PlayerPrefs.HasKey(key), integer = PlayerPrefs.GetInt(key), number = PlayerPrefs.GetFloat(key) });
    }
    void Restore()
    {
        if (_probe != null) Destroy(_probe);
        if (_probeMaterial != null) Destroy(_probeMaterial);
        QualitySettings.SetQualityLevel(_quality, true);
        Screen.SetResolution(_width, _height, _mode);
        PauseGame.menuActive = _pause;
        foreach (var behaviour in _disabled) if (behaviour != null) behaviour.enabled = true;
        foreach (var p in _preferences)
        {
            if (!p.present) PlayerPrefs.DeleteKey(p.key);
            else if (p.floating) PlayerPrefs.SetFloat(p.key, p.number);
            else PlayerPrefs.SetInt(p.key, p.integer);
        }
        PlayerPrefs.Save(); GraphicsPreferences.Changed();
        GraphicsPreferences.SetRenderScale(_renderScale);
    }
    IEnumerator Start()
    {
        Capture();
        GraphicsPreferences.SetRenderScale(1f);
        var test = Run();
        bool running = true;
        while (running)
        {
            object next = null;
            try { running = test.MoveNext(); if (running) next = test.Current; }
            catch (Exception e) { _report.error = e.ToString(); running = false; }
            if (running) yield return next;
        }
        Restore();
        _report.passed = string.IsNullOrEmpty(_report.error);
        Directory.CreateDirectory("Logs");
        File.WriteAllText("Logs/graphics-verification.json", JsonUtility.ToJson(_report, true));
        if (_report.passed) Debug.Log("[Graphics verification] PASS: " + string.Join("; ", _report.checks));
        else Debug.LogError("[Graphics verification] FAIL: " + _report.error);
        if (Environment.GetCommandLineArgs().Contains("-verifyGraphics")
            && (!_report.passed || !Environment.GetCommandLineArgs().Contains("-keepVerificationHost"))) Application.Quit(_report.passed ? 0 : 1);
        else Destroy(gameObject);
    }
    static void MenuCall(SettingsMenu menu, string method, params object[] args)
    {
        typeof(SettingsMenu).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).Invoke(menu, args);
    }
    void VerifySingleShot()
    {
        var shoot = GameManager.LocalPlayer.GetComponent<PlayerShoot>();
        var weapon = shoot.Equipment.getActiveWeapon();
        int mag = weapon.CurrentMagAmmo, ammo = weapon.CurrentAmmo;
        var mode = weapon.Mode;
        var state = weapon.State;
        var latch = typeof(PlayerShoot).GetField("_shootingDone", BindingFlags.Instance | BindingFlags.NonPublic);
        bool latched = (bool)latch.GetValue(shoot);
        try
        {
            weapon.Mode = Weapon.FireMode.single;
            latch.SetValue(shoot, false);
            var fire = typeof(PlayerShoot).GetMethod("Shoot", BindingFlags.Instance | BindingFlags.NonPublic);
            fire.Invoke(shoot, null);
            Require(weapon.CurrentMagAmmo == mag - 1, "Single pistol click must use one round");
            fire.Invoke(shoot, null);
            Require(weapon.CurrentMagAmmo == mag - 1, "Held single-mode trigger must not repeat");
        }
        finally
        {
            weapon.CurrentMagAmmo = mag; weapon.CurrentAmmo = ammo;
            weapon.Mode = mode; weapon.State = state; latch.SetValue(shoot, latched);
        }
    }
    void VerifyBloomPixels(RuntimeGraphics owner)
    {
        var go = new GameObject("Bloom pixel probe", typeof(Camera));
        var camera = go.GetComponent<Camera>(); camera.enabled = false;
        camera.transform.position = new Vector3(10000, 10000, 10000);
        camera.orthographic = true; camera.orthographicSize = 1;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.cullingMask = 1 << 2; camera.allowHDR = true;
        var target = new RenderTexture(128, 128, 16, RenderTextureFormat.ARGBHalf);
        var pixels = new Texture2D(128, 128, TextureFormat.RGBAFloat, false, true);
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad); quad.layer = 2;
        quad.transform.position = camera.transform.position + Vector3.forward;
        quad.transform.localScale = Vector3.one * 0.4f;
        var material = new Material(Resources.Load<Material>("Rendering/ExposureProbe"));
        material.SetColor("_BaseColor", Color.white * 16); quad.GetComponent<Renderer>().sharedMaterial = material;
        bool automatic = GraphicsPreferences.AutoExposure;
        float exposure = GraphicsPreferences.ManualExposure;
        var previous = RenderTexture.active;
        try
        {
            GraphicsPreferences.AutoExposure = false; GraphicsPreferences.ManualExposure = 0;
            camera.targetTexture = target;
            var data = UrpAccess.CameraData(camera);
            UrpAccess.Set(data, "renderPostProcessing", true);
            UrpAccess.Set(data, "volumeLayerMask", (LayerMask)(1 << LayerMask.NameToLayer("PostFX World")));
            float[] sums = new float[2];
            for (int pass = 0; pass < 2; pass++)
            {
                GraphicsPreferences.Bloom = pass == 1;
                owner.Apply();
                UrpAccess.Parameter(UrpAccess.Effect(owner.WorldProfile, "ColorAdjustments"), "postExposure", 0f);
                camera.Render(); RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 128, 128), 0, 0); pixels.Apply();
                Color[] samples = pixels.GetPixels();
                for (int y = 32; y < 96; y++) for (int x = 32; x < 96; x++)
                    if (x < 46 || x > 82 || y < 46 || y > 82) sums[pass] += samples[y * 128 + x].r;
            }
            Require(sums[1] > sums[0] + 0.1f, "Bloom toggle did not change surrounding rendered pixels: " + sums[0] + "/" + sums[1]);
            _report.checks.Add("GPU bloom halo pixels off=" + sums[0] + ", on=" + sums[1]);
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null;
            target.Release(); Destroy(target); Destroy(pixels); Destroy(quad); Destroy(material); Destroy(go);
            GraphicsPreferences.AutoExposure = automatic; GraphicsPreferences.ManualExposure = exposure;
            GraphicsPreferences.Bloom = true; owner.Apply();
        }
    }
    IEnumerator Run()
    {
        SceneManager.LoadScene("LobbyTests");
        float deadline = Time.realtimeSinceStartup + 45;
        while (CustomNetworkManager.Instance == null) { Require(Time.realtimeSinceStartup < deadline, "Lobby timeout"); yield return null; }
        CustomNetworkManager.Instance.Host("Graphics verification", 7799);
        while (GameManager.LocalPlayer == null || RuntimeGraphics.Instance == null) { Require(Time.realtimeSinceStartup < deadline, "Player timeout"); yield return null; }
        foreach (var screens in UnityEngine.Object.FindObjectsByType<ButtonsControll>()) { screens.SkipWelcomeScreen(); screens.SkipControls(); }
        yield return new WaitForSecondsRealtime(1);
        _width = Screen.width; _height = Screen.height; _mode = Screen.fullScreenMode;
        foreach (var type in new[] { typeof(PlayerController), typeof(PlayerMotor), typeof(PlayerShoot), typeof(PlayerEquipment), typeof(PlacementController) })
        {
            var behaviour = GameManager.LocalPlayer.GetComponent(type) as Behaviour;
            if (behaviour != null && behaviour.enabled) { _disabled.Add(behaviour); behaviour.enabled = false; }
        }
        var owner = RuntimeGraphics.Instance;
        var source = GameManager.LocalPlayer.GetComponent<PlayerSetup>().ActionCamera;
        var weapon = UrpCameraStack.WeaponCamera(source);
        Require(weapon != null && weapon.targetTexture == null, "Weapon must render into the native camera stack");
        var stack = UrpAccess.Get(UrpAccess.CameraData(source), "cameraStack") as List<Camera>;
        Require(stack != null && stack.Contains(weapon), "Local weapon camera missing from world stack");
        Require(UrpAccess.Get(UrpAccess.CameraData(weapon), "renderType").ToString() == "Overlay", "Weapon must be Overlay");
        Require(!(bool)UrpAccess.Get(UrpAccess.CameraData(source), "renderPostProcessing")
            && (bool)UrpAccess.Get(UrpAccess.CameraData(weapon), "renderPostProcessing"), "Shared post effects must run once");
        VerifySingleShot();
        _report.checks.Add("Native Base/Overlay stack; single-click pistol consumes one round and holding does not repeat");
        foreach (var pair in new[] { "Bloom:Bloom", "Vignette:Vignette", "DepthOfField:DepthOfField", "MotionBlur:MotionBlur", "Tonemapping:Tonemapping", "ChromaticAberration:ChromaticAberration" })
        {
            var names = pair.Split(':');
            var setting = typeof(GraphicsPreferences).GetProperty(names[0]);
            foreach (bool enabled in new[] { false, true })
            {
                setting.SetValue(null, enabled, null);
                owner.Apply();
                yield return null;
                Require((bool)UrpAccess.Get(UrpAccess.Effect(owner.WorldProfile, names[1]), "active") == enabled, "Effect toggle failed: " + names[0]);
            }
        }
        _report.checks.Add("Every shared Volume toggle applies live");
        foreach (bool enabled in new[] { false, true })
        {
            GraphicsPreferences.AmbientOcclusion = enabled;
            owner.Apply();
            yield return null;
            var data = (Array)UrpAccess.Get(QualitySettings.renderPipeline, "m_RendererDataList");
            Require(data.Length == 1, "Obsolete weapon renderer remains assigned");
            Require(UrpAccess.Get(data.GetValue(0), "renderingMode").ToString() == "ForwardPlus", "Forward+ not selected");
            var features = (IList)UrpAccess.Get(data.GetValue(0), "rendererFeatures");
            Require(features.Cast<object>().Any(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion" && (bool)UrpAccess.Get(f, "isActive") == enabled), "Shared AO toggle failed");
        }
        for (int aa = 0; aa < 2; aa++)
        {
            GraphicsPreferences.AntiAliasing = aa;
            GraphicsPreferences.Dithering = aa == 1;
            owner.Apply();
            yield return null;
            string expected = aa == 0 ? "None" : "FastApproximateAntialiasing";
            Require(UrpAccess.Get(UrpAccess.CameraData(source), "antialiasing").ToString() == expected, "Base camera did not supply shared AA to the stack");
            Require(UrpAccess.Get(UrpAccess.CameraData(weapon), "antialiasing").ToString() == expected, "Shared AA mode failed");
            Require((bool)UrpAccess.Get(UrpAccess.CameraData(source), "dithering") == (aa == 1), "Base camera did not supply dithering to the stack");
            Require((bool)UrpAccess.Get(UrpAccess.CameraData(weapon), "dithering") == (aa == 1), "Dithering toggle failed");
        }
        _report.checks.Add("Shared AO, Off/FXAA and dithering apply; Forward+ and native render scale selected");
        foreach (int level in Enumerable.Range(0, QualitySettings.names.Length))
        {
            GameSettings.QualityLevel = level;
            yield return new WaitForSecondsRealtime(0.4f);
            Require(QualitySettings.vSyncCount == 0, "Quality change enabled V-Sync over the frame limit");
            Require(Application.targetFrameRate == (GameSettings.MaxFps > 0 ? GameSettings.MaxFps : -1), "Quality change lost the frame limit");
            Require(QualitySettings.renderPipeline.name.Contains("Clone"), "Quality change lost runtime pipeline ownership");
            Require(Mathf.Approximately((float)UrpAccess.Get(QualitySettings.renderPipeline, "renderScale"), 1f), "Native render scale lost on quality change");
            var renderer = ((Array)UrpAccess.Get(QualitySettings.renderPipeline, "m_RendererDataList")).GetValue(0);
            Require(UrpAccess.Get(renderer, "renderingMode").ToString() == "ForwardPlus", "Forward+ lost on quality change");
        }
        Require((bool)UrpAccess.Get(UrpAccess.Effect(Resources.Load<ScriptableObject>("Rendering/WorldEffects"), "Bloom"), "active"), "Settings mutated the source profile");
        _report.checks.Add("Quality switching preserves settings; source profile assets remain unchanged");
        GraphicsPreferences.AutoExposure = true;
        if (SystemInfo.supportsAsyncGPUReadback)
        {
            _probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _probe.transform.SetPositionAndRotation(source.transform.position + source.transform.forward, source.transform.rotation);
            _probe.transform.localScale = Vector3.one * 4;
            _probe.GetComponent<Collider>().enabled = false;
            _probeMaterial = new Material(Resources.Load<Material>("Rendering/ExposureProbe"));
            _probe.GetComponent<Renderer>().sharedMaterial = _probeMaterial;
            _probeMaterial.SetColor("_BaseColor", Color.white);
            yield return new WaitForSecondsRealtime(2);
            var adaptation = owner.GetComponent<AdaptiveExposure>();
            float bright = (float)UrpAccess.Get(adaptation, "_targetExposure");
            _probeMaterial.SetColor("_BaseColor", Color.black);
            yield return new WaitForSecondsRealtime(2);
            float dark = (float)UrpAccess.Get(adaptation, "_targetExposure");
            Require(dark > bright + 1, "Exposure failed to adapt to bright/dark geometry: " + bright + " / " + dark);
            _report.checks.Add("GPU eye adaptation: bright=" + bright + " EV, dark=" + dark + " EV");
            Destroy(_probe); Destroy(_probeMaterial);
        }
        GraphicsPreferences.Bloom = GraphicsPreferences.Vignette = GraphicsPreferences.AutoExposure = true;
        GraphicsPreferences.DepthOfField = GraphicsPreferences.MotionBlur = GraphicsPreferences.Tonemapping = GraphicsPreferences.ChromaticAberration = GraphicsPreferences.Dithering = GraphicsPreferences.AmbientOcclusion = false;
        GraphicsPreferences.AntiAliasing = 1;
        owner.Apply();
        yield return null;
        VerifyBloomPixels(owner);
        VerifyPlaceableRendering();
        var menu = UnityEngine.Object.FindObjectsByType<SettingsMenu>().First();
        PauseGame.menuActive = true;
        MenuCall(menu, "OpenWindow");
        UnityEngine.Object.FindFirstObjectByType<SettingsMenuView>().ShowTab(2);
        if (!Application.isEditor)
        {
            MenuCall(menu, "ApplyDisplay", 640, 360, false);
            yield return new WaitForSecondsRealtime(1);
            Require(Screen.width == 640 && Screen.height == 360, "Windowed resolution was not applied");
            var panel = (RectTransform)UnityEngine.Object.FindFirstObjectByType<SettingsMenuView>().transform.Find("Panel");
            var corners = new Vector3[4]; panel.GetWorldCorners(corners);
            Require(corners.All(c => c.x >= 0 && c.y >= 0 && c.x <= Screen.width + 1 && c.y <= Screen.height + 1), "Settings panel overflows 640x360");
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Logs/settings-effects-640.png"));
            yield return new WaitForEndOfFrame();
            yield return null;
            typeof(SettingsMenu).GetField("_displayDeadline", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(menu, Time.realtimeSinceStartup - 1);
            yield return new WaitForSecondsRealtime(0.5f);
            Require(Screen.width == _width && Screen.height == _height, "Resolution timeout did not restore the previous display");
            MenuCall(menu, "ApplyDisplay", 640, 360, false);
            yield return new WaitForSecondsRealtime(0.5f);
            MenuCall(menu, "ConfirmDisplay");
            Require(PlayerPrefs.GetInt("display.width") == 640 && PlayerPrefs.GetInt("display.height") == 360, "Confirmed resolution was not persisted");
            Screen.SetResolution(_width, _height, _mode);
            yield return new WaitForSecondsRealtime(0.5f);
            GraphicsPreferences.ApplySavedDisplay();
            yield return new WaitForSecondsRealtime(0.5f);
            Require(Screen.width == 640 && Screen.height == 360, "Saved display settings were not reapplied");
            _report.checks.Add("Native 640x360 resolution, confirmation persistence/reapply, timed rollback and small-screen UI pass");
        }
        else _report.checks.Add("Native resolution change checks deferred to player build");
        MenuCall(menu, "CloseWindow");
        Require(PauseGame.menuActive, "Closing Settings resumed gameplay");
        _report.checks.Add("Closing Settings leaves pause active");
        var manager = GameManager.Instance;
        var oldPhase = manager.CurrentMachState;
        var oldRoom = manager.CurrentRoom;
        bool oldEnabled = manager.enabled, oldCounting = ClockManager.canCount;
        float oldTime = ClockManager.time;
        var nightProbe = RenderSettings.ambientProbe;
        float nightIntensity = RenderSettings.ambientIntensity;
        bool nightFog = RenderSettings.fog;
        var nightFogMode = RenderSettings.fogMode;
        Color nightFogColor = RenderSettings.fogColor;
        float nightFogDensity = RenderSettings.fogDensity;
        manager.enabled = false;
        try
        {
            foreach (var pair in new[] {
                new[] { GameManager.MatchState.Room1Prepare, GameManager.MatchState.Room1Fight },
                new[] { GameManager.MatchState.Room2Prepare, GameManager.MatchState.Room2Fight },
                new[] { GameManager.MatchState.Room3Prepare, GameManager.MatchState.Room3Fight } })
            {
                manager.CurrentMachState = pair[0];
                yield return new WaitForSecondsRealtime(0.15f);
                Require(RenderSettings.fogColor == nightFogColor && Mathf.Abs(RenderSettings.fogDensity - nightFogDensity) < 0.0001f, "Preparation did not reset candle fog");
                manager.CurrentMachState = pair[1];
                manager.StopHordeAttack(); ClockManager.canCount = false;
                yield return new WaitForSecondsRealtime(0.3f);
                Require(Mathf.Abs(RenderSettings.ambientIntensity - nightIntensity) < 0.001f && Mathf.Abs(RoomLighting.ExposureOffset) < 0.001f, "Full candles changed night brightness");
                for (int channel = 0; channel < 3; channel++)
                    for (int coefficient = 0; coefficient < 9; coefficient++)
                        Require(Mathf.Abs(RenderSettings.ambientProbe[channel, coefficient] - nightProbe[channel, coefficient]) < 0.0001f, "Preparation/fight transition amplified the ambient probe");
                Require(RenderSettings.fogColor == nightFogColor && Mathf.Abs(RenderSettings.fogDensity - nightFogDensity) < 0.0001f, "Full candles changed the original fog");
                var area = manager.CurrentRoom.CaptureAreas.Last();
                float progress = area._progress;
                using var fogDraws = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Draw Calls Count");
                using var fogTextures = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Render, "Render Textures Count");
                try
                {
                    yield return new WaitForSecondsRealtime(0.2f);
                    long drawsBefore = fogDraws.Valid ? fogDraws.LastValue : -1;
                    long texturesBefore = fogTextures.Valid ? fogTextures.LastValue : -1;
                    area._progress = 50;
                    yield return new WaitForSecondsRealtime(1.5f);
                    if (nightFog && nightFogMode == FogMode.ExponentialSquared)
                    {
                        Require(RenderSettings.fogColor.maxColorComponent < nightFogColor.maxColorComponent, "Half candles did not darken fog");
                        Require(RenderSettings.fogDensity >= nightFogDensity && RenderSettings.fogDensity <= Mathf.Max(nightFogDensity, 0.12f), "Candle fog density outside bounds");
                    }
                    Require(RenderSettings.fog == nightFog && RenderSettings.fogMode == nightFogMode, "Candle effect changed the fog mode or enabled flag");
                    _report.checks.Add("Candle fog " + manager.CurrentRoom.name + ": draws " + drawsBefore + " -> " + (fogDraws.Valid ? fogDraws.LastValue : -1)
                        + ", render textures " + texturesBefore + " -> " + (fogTextures.Valid ? fogTextures.LastValue : -1));
                }
                finally { area._progress = progress; }
            }
            _report.checks.Add("All three preparation/fight transitions preserve the original night ambient probe and intensity");
            manager.CurrentMachState = GameManager.MatchState.Win;
            yield return new WaitForSecondsRealtime(0.15f);
            Require(RenderSettings.fogColor == nightFogColor && Mathf.Abs(RenderSettings.fogDensity - nightFogDensity) < 0.0001f, "Victory did not restore original fog");
            _report.checks.Add("Candle fog darkens in all three rooms, stays bounded and restores on preparation/victory without changing fog mode");
        }
        finally
        {
            manager.CurrentMachState = oldPhase;
            typeof(GameManager).GetField("_currentRoom", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(manager, oldRoom);
            manager.enabled = oldEnabled;
            ClockManager.time = oldTime; ClockManager.canCount = oldCounting;
        }
        _report.checks.Add("PASS");
    }

    void VerifyPlaceableRendering()
    {
        var placement = GameManager.LocalPlayer.GetComponent<PlacementController>();
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var models = (List<GameObject>)typeof(PlacementController).GetField("_placeableObjectModels", flags).GetValue(placement);
        var prefabs = (List<GameObject>)typeof(PlacementController).GetField("_placeableObject", flags).GetValue(placement);
        var cameraObject = new GameObject("Placeable render probe", typeof(Camera));
        var lightObject = new GameObject("Placeable probe light", typeof(Light));
        var target = new RenderTexture(384, 288, 24);
        var pixels = new Texture2D(384, 288, TextureFormat.RGB24, false);
        var camera = cameraObject.GetComponent<Camera>();
        camera.enabled = false; camera.cullingMask = 1 << 30;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.gray;
        camera.nearClipPlane = 0.01f; camera.farClipPlane = 100f; camera.targetTexture = target;
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1f; light.cullingMask = 1 << 30;
        light.transform.rotation = Quaternion.Euler(45, 30, 0);
        var previous = RenderTexture.active;
        try
        {
            foreach (var prefab in models.Concat(prefabs))
            {
                var clone = Instantiate(prefab, new Vector3(1000, 1000, 1000), Quaternion.identity);
                try
                {
                    foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                    foreach (var sprite in clone.GetComponentsInChildren<SpriteRenderer>()) sprite.enabled = false;
                    foreach (var transform in clone.GetComponentsInChildren<Transform>()) transform.gameObject.layer = 30;
                    var renderers = clone.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
                    Require(renderers.Length > 0, prefab.name + ": no visible model");
                    Require(renderers.SelectMany(r => r.sharedMaterials).All(m => m != null && m.shader != null && m.shader.isSupported && m.shader.name != "Standard" && m.shader.name != "Hidden/InternalErrorShader"), prefab.name + ": unsupported material");
                    var bounds = renderers[0].bounds;
                    foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                    for (int view = 0; view < 2; view++)
                    {
                        float direction = view == 0 ? 1 : -1;
                        camera.transform.position = bounds.center + new Vector3(direction, 1, -direction).normalized * bounds.size.magnitude * 1.1f;
                        camera.transform.LookAt(bounds.center);
                        camera.Render(); RenderTexture.active = target;
                        pixels.ReadPixels(new Rect(0, 0, 384, 288), 0, 0); pixels.Apply();
                        var colors = pixels.GetPixels(); Color background = colors[0];
                        int visible = colors.Count(c => Mathf.Abs(c.r - background.r) + Mathf.Abs(c.g - background.g) + Mathf.Abs(c.b - background.b) > 0.1f);
                        Require(visible > 100, prefab.name + ": model disappeared in view " + view);
                        File.WriteAllBytes("Logs/placeable-" + prefab.name + "-" + view + ".png", pixels.EncodeToPNG());
                    }
                }
                finally { DestroyImmediate(clone); }
            }
            _report.checks.Add("All three previews and placed models render pixels from both directions with supported materials");
        }
        finally
        {
            RenderTexture.active = previous; target.Release();
            Destroy(target); Destroy(pixels); Destroy(cameraObject); Destroy(lightObject);
        }
    }
}

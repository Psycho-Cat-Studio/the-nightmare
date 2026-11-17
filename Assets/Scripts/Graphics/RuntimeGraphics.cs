using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Owns runtime copies; opening settings never edits the project's render-pipeline/profile assets.
public class RuntimeGraphics : MonoBehaviour
{
    public static RuntimeGraphics Instance { get; private set; }
    public ScriptableObject WorldProfile { get; private set; }
    public float Exposure { get; set; } = 1f;
    readonly Dictionary<int, RenderPipelineAsset> _originals = new Dictionary<int, RenderPipelineAsset>();
    readonly Dictionary<int, RenderPipelineAsset> _pipelines = new Dictionary<int, RenderPipelineAsset>();
    readonly List<UnityEngine.Object> _owned = new List<UnityEngine.Object>();
    int _revision = -1;
    float _nextScan;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Create()
    {
        if (GraphicsSettings.currentRenderPipeline == null) return;
        var go = new GameObject("Runtime Graphics");
        DontDestroyOnLoad(go);
        go.AddComponent<RuntimeGraphics>();
    }
    ScriptableObject Profile(string path, string layer)
    {
        var original = Resources.Load<ScriptableObject>(path);
        if (original == null) throw new InvalidOperationException("Missing rendering profile: " + path);
        var clone = Instantiate(original);
        _owned.Add(clone);
        var components = (IList)UrpAccess.Get(clone, "components");
        for (int i = 0; i < components.Count; i++)
        {
            components[i] = Instantiate((UnityEngine.Object)components[i]);
            _owned.Add((UnityEngine.Object)components[i]);
        }
        var go = new GameObject(layer);
        go.layer = LayerMask.NameToLayer(layer);
        go.transform.SetParent(transform);
        var type = System.Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime", true);
        var volume = go.AddComponent(type);
        UrpAccess.Set(volume, "isGlobal", true);
        UrpAccess.Set(volume, "priority", 100f);
        UrpAccess.Set(volume, "sharedProfile", clone);
        return clone;
    }
    void Awake()
    {
        Instance = this;
        WorldProfile = Profile("Rendering/WorldEffects", "PostFX World");
        gameObject.AddComponent<AdaptiveExposure>();
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }
    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Refresh the sky environment after retiring stale Built-in LightingData.
        // This updates ambient SH/reflections; it does not enable Enlighten realtime GI.
        if (RenderSettings.ambientMode == AmbientMode.Skybox) DynamicGI.UpdateEnvironment();
        Apply();
    }
    void Update()
    {
        if (_revision != GraphicsPreferences.Revision || Time.unscaledTime >= _nextScan)
        {
            Apply();
            _nextScan = Time.unscaledTime + 0.5f;
        }
        UrpAccess.Parameter(UrpAccess.Effect(WorldProfile, "ColorAdjustments"), "postExposure", GraphicsPreferences.AutoExposure && SystemInfo.supportsAsyncGPUReadback ? Exposure : GraphicsPreferences.ManualExposure);
    }
    RenderPipelineAsset Pipeline()
    {
        int quality = QualitySettings.GetQualityLevel();
        RenderPipelineAsset clone;
        if (!_pipelines.TryGetValue(quality, out clone))
        {
            var original = QualitySettings.renderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            _originals.Add(quality, original);
            clone = Instantiate(original);
            _pipelines.Add(quality, clone);
            _owned.Add(clone);
            var data = (Array)UrpAccess.Get(clone, "m_RendererDataList");
            for (int i = 0; i < data.Length; i++)
            {
                var renderer = Instantiate((UnityEngine.Object)data.GetValue(i));
                _owned.Add(renderer);
                var features = (IList)UrpAccess.Get(renderer, "rendererFeatures");
                for (int f = 0; f < features.Count; f++)
                {
                    if (features[f] == null) continue;
                    features[f] = Instantiate((UnityEngine.Object)features[f]);
                    _owned.Add((UnityEngine.Object)features[f]);
                }
                data.SetValue(renderer, i);
            }
            QualitySettings.renderPipeline = clone;
        }
        return clone;
    }
    static void Active(ScriptableObject profile, string effect, bool value)
    {
        UrpAccess.Set(UrpAccess.Effect(profile, effect), "active", value);
    }
    public void Apply()
    {
        if (WorldProfile == null) return;
        var pipeline = Pipeline();
        UrpAccess.Set(pipeline, "renderScale", 1f);
        UrpAccess.Set(pipeline, "upscalingFilter", "Linear");
        UrpAccess.Set(pipeline, "msaaSampleCount", 1);
        Active(WorldProfile, "Bloom", GraphicsPreferences.Bloom);
        Active(WorldProfile, "Vignette", GraphicsPreferences.Vignette);
        Active(WorldProfile, "DepthOfField", GraphicsPreferences.DepthOfField);
        Active(WorldProfile, "MotionBlur", GraphicsPreferences.MotionBlur);
        Active(WorldProfile, "Tonemapping", GraphicsPreferences.Tonemapping);
        Active(WorldProfile, "ChromaticAberration", GraphicsPreferences.ChromaticAberration);
        foreach (var renderer in (Array)UrpAccess.Get(pipeline, "m_RendererDataList"))
            foreach (var feature in (IList)UrpAccess.Get(renderer, "rendererFeatures"))
                if (feature != null && feature.GetType().Name == "ScreenSpaceAmbientOcclusion")
                    feature.GetType().GetMethod("SetActive").Invoke(feature, new object[] { GraphicsPreferences.AmbientOcclusion });
        UrpCameraStack.Restack();
        ConfigureCameras();
        _revision = GraphicsPreferences.Revision;
    }
    void ConfigureCameras()
    {
        var player = GameManager.LocalPlayer;
        var setup = player == null ? null : player.GetComponent<PlayerSetup>();
        Camera action = setup == null ? null : setup.ActionCamera;
        Camera weapon = action == null ? null : UrpCameraStack.WeaponCamera(action);
        // Use the local player's cameras, never a remote player's identically named objects.
        foreach (Camera camera in Camera.allCameras)
        {
            if (camera.name == "Exposure Meter") continue;
            var data = UrpAccess.CameraData(camera);
            bool gameplay = player != null && camera.transform.IsChildOf(player.transform)
                && camera.targetTexture == null && camera.cullingMask != (1 << LayerMask.NameToLayer("UI"));
            bool overlay = string.Equals(UrpAccess.Get(data, "renderType").ToString(), "Overlay");
            // A single shared pass on the last 3D camera. Canvas UI is composed afterwards.
            bool post = gameplay && (camera == weapon || !overlay && (weapon == null || !weapon.isActiveAndEnabled));
            UrpAccess.Set(data, "volumeLayerMask", (LayerMask)(gameplay ? 1 << LayerMask.NameToLayer("PostFX World") : 0));
            UrpAccess.Set(data, "renderPostProcessing", post);
            // URP initializes AA/dithering for the whole stack from its Base camera, even when
            // post-processing itself runs only on the last Overlay. Keep both camera roles aligned.
            UrpAccess.Set(data, "dithering", gameplay && GraphicsPreferences.Dithering);
            UrpAccess.Set(data, "antialiasing", gameplay && GraphicsPreferences.AntiAliasing == 1 ? "FastApproximateAntialiasing" : "None");
            UrpAccess.Set(data, "antialiasingQuality", "High");
        }
    }
    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        var restore = typeof(QualitySettings).GetMethod("SetRenderPipelineAssetAt", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        foreach (var pair in _originals)
            if (restore != null) restore.Invoke(null, new object[] { pair.Key, pair.Value });
        foreach (var obj in _owned) if (obj != null) Destroy(obj);
        if (Instance == this) Instance = null;
    }
}

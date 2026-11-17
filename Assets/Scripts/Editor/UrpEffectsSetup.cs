using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Profiles intentionally contain every switchable effect so player builds retain their variants.
public static class UrpEffectsSetup
{
    static ScriptableObject Profile(string path)
    {
        var profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance(Type.GetType("UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime", true));
            AssetDatabase.CreateAsset(profile, path);
        }
        return profile;
    }
    static object Effect(ScriptableObject profile, string name)
    {
        var components = (IList)UrpAccess.Get(profile, "components");
        foreach (var c in components) if (c.GetType().Name == name) return c;
        var effect = ScriptableObject.CreateInstance(UrpAccess.Type(name));
        effect.name = name;
        components.Add(effect);
        AssetDatabase.AddObjectToAsset(effect, profile);
        return effect;
    }
    static void Layer(string name)
    {
        if (LayerMask.NameToLayer(name) >= 0) return;
        var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tags.FindProperty("layers");
        for (int i = 31; i >= 8; i--)
            if (string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
            {
                layers.GetArrayElementAtIndex(i).stringValue = name;
                tags.ApplyModifiedPropertiesWithoutUndo();
                return;
            }
        throw new InvalidOperationException("A free user layer is required for " + name);
    }
    [MenuItem("The Nightmare/URP/Configure switchable effects")]
    public static void Configure()
    {
        Layer("PostFX World");
        var world = Profile("Assets/Resources/Rendering/WorldEffects.asset");
        UrpAccess.Parameter(Effect(world, "Bloom"), "intensity", 0.2f);
        UrpAccess.Parameter(Effect(world, "Bloom"), "threshold", 1.1f);
        UrpAccess.Parameter(Effect(world, "Vignette"), "intensity", 0.45f);
        UrpAccess.Parameter(Effect(world, "Vignette"), "smoothness", 0.2f);
        UrpAccess.Parameter(Effect(world, "ColorAdjustments"), "postExposure", 1f);
        UrpAccess.Parameter(Effect(world, "DepthOfField"), "mode", "Bokeh");
        UrpAccess.Parameter(Effect(world, "DepthOfField"), "focusDistance", 10f);
        UrpAccess.Parameter(Effect(world, "DepthOfField"), "aperture", 26f);
        UrpAccess.Parameter(Effect(world, "DepthOfField"), "focalLength", 50f);
        UrpAccess.Parameter(Effect(world, "MotionBlur"), "mode", "CameraAndObjects");
        UrpAccess.Parameter(Effect(world, "MotionBlur"), "intensity", 0.75f);
        UrpAccess.Parameter(Effect(world, "Tonemapping"), "mode", "Neutral");
        UrpAccess.Parameter(Effect(world, "ChromaticAberration"), "intensity", 0.1f);
        EditorUtility.SetDirty(world);
        foreach (var component in (IList)UrpAccess.Get(world, "components")) EditorUtility.SetDirty((UnityEngine.Object)component);
        var renderer = AssetDatabase.LoadAssetAtPath<ScriptableObject>("Assets/URPDefaultResources/Default_Forward_Renderer.asset");
        UrpAccess.Set(renderer, "renderingMode", "ForwardPlus");
        var features = (IList)UrpAccess.Get(renderer, "rendererFeatures");
        var feature = features.Cast<object>().FirstOrDefault(f => f != null && f.GetType().Name == "ScreenSpaceAmbientOcclusion") as ScriptableObject;
        if (feature == null)
        {
            feature = ScriptableObject.CreateInstance(UrpAccess.Type("ScreenSpaceAmbientOcclusion"));
            feature.name = "Ambient occlusion";
            AssetDatabase.AddObjectToAsset(feature, renderer);
            features.Add(feature);
        }
        var settings = UrpAccess.Get(feature, "m_Settings");
        UrpAccess.Set(settings, "Intensity", 1f);
        UrpAccess.Set(settings, "Radius", 0.3f);
        UrpAccess.Set(settings, "Downsample", true);
        UrpAccess.Set(settings, "AfterOpaque", true);
        UrpAccess.Set(settings, "Source", "DepthNormals");
        UrpAccess.Set(settings, "Samples", "Medium");
        feature.GetType().GetMethod("Create").Invoke(feature, null);
        EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer);
        var serializedRenderer = new SerializedObject(renderer);
        var featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
        featureMap.arraySize = features.Count;
        for (int i = 0; i < features.Count; i++)
        {
            string guid; long localId;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier((UnityEngine.Object)features[i], out guid, out localId);
            featureMap.GetArrayElementAtIndex(i).longValue = localId;
        }
        serializedRenderer.ApplyModifiedPropertiesWithoutUndo();
        foreach (string path in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset", new[] { "Assets/URPDefaultResources" }).Select(AssetDatabase.GUIDToAssetPath))
        {
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            var serialized = new SerializedObject(asset);
            var data = serialized.FindProperty("m_RendererDataList");
            data.arraySize = 1;
            data.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            serialized.FindProperty("m_AllowPostProcessAlphaOutput").boolValue = false;
            serialized.FindProperty("m_MSAA").intValue = 1;
            serialized.FindProperty("m_RenderScale").floatValue = 1f;
            serialized.FindProperty("m_UpscalingFilter").intValue = 1; // Bilinear; no automatic upscaler selection.
            serialized.FindProperty("m_AdditionalLightsRenderingMode").intValue = 1;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        const string exposureProbe = "Assets/Resources/Rendering/ExposureProbe.mat";
        if (AssetDatabase.LoadAssetAtPath<Material>(exposureProbe) == null)
            AssetDatabase.CreateAsset(new Material(Shader.Find("Universal Render Pipeline/Unlit")), exposureProbe);
        AssetDatabase.SaveAssets();
        Debug.Log("[URP] Shared effects profile and Forward+ renderer saved.");
    }
}

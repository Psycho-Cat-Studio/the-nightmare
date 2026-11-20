using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Checks and repairs rendering assets for the committed Built-in-to-URP migration.</summary>
public static class UrpMigrationAudit
{
    [MenuItem("The Nightmare/URP/Validate build assets")]
    public static void ValidateBuild()
    {
        if (GraphicsSettings.defaultRenderPipeline == null || !GraphicsSettings.defaultRenderPipeline.GetType().Name.Contains("UniversalRenderPipelineAsset"))
            throw new InvalidOperationException("Assign the committed URP pipeline before building.");
        var paths = EditorBuildSettings.scenes.Where(x => x.enabled).Select(x => x.path)
            .SelectMany(p => AssetDatabase.GetDependencies(p, true))
            .Concat(AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).Where(p => p.Contains("/Resources/")))
            .Where(p => p.EndsWith(".mat") || AssetImporter.GetAtPath(p) is ModelImporter).Distinct().ToArray();
        var errors = new System.Collections.Generic.List<string>();
        int materialCount = 0;
        foreach (string path in paths)
        foreach (var mat in AssetDatabase.LoadAllAssetsAtPath(path).OfType<Material>())
        {
            materialCount++;
            if (mat == null || mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
            { errors.Add(path + ": missing shader"); continue; }
            string shader = mat.shader.name;
            if (shader == "Standard" || shader.StartsWith("Legacy Shaders/") || shader.StartsWith("Mobile/Particles/")
                || shader.StartsWith("High Quality Bricks & Walls/") || shader == "custom/Cutout/Bumped Diffuse")
                errors.Add(path + ": built-in shader " + shader);
            if (ShaderUtil.ShaderHasError(mat.shader)) errors.Add(path + ": shader compiler errors");
        }
        foreach (string name in new[] { "DissolverShader/DissolveShader", "Custom/Outline Fill", "Custom/Outline Mask", "Custom/Explosion", "Unity5/Particle Softrim Dissolve/Alpha Blend" })
        {
            var shader = Shader.Find(name);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) errors.Add(name + ": custom shader missing or invalid");
        }
        System.IO.Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/urp-validation.txt", new[] { "URP materials checked: " + materialCount, "Errors: " + errors.Count }.Concat(errors));
        if (errors.Count != 0) throw new InvalidOperationException(string.Join("\n", errors));
        Debug.Log("[URP] Validated " + materialCount + " build/resource materials.");
    }

    [MenuItem("The Nightmare/URP/Audit")]
    public static void Audit()
    {
        var lines = new System.Collections.Generic.List<string>();
        lines.Add("Pipeline: " + GraphicsSettings.defaultRenderPipeline);
        foreach (var group in AssetDatabase.FindAssets("t:Material", new[] { "Assets" })
            .Select(AssetDatabase.GUIDToAssetPath).Select(p => new { path = p, mat = AssetDatabase.LoadAssetAtPath<Material>(p) })
            .Where(x => x.mat != null).GroupBy(x => x.mat.shader == null ? "MISSING" : x.mat.shader.name))
            lines.Add(group.Key + " | " + group.Count() + " | " + string.Join(";", group.Take(3).Select(x => x.path)));
        foreach (var scene in EditorBuildSettings.scenes.Where(s => s.enabled))
        {
            lines.Add("SCENE " + scene.path);
            foreach (string p in AssetDatabase.GetDependencies(scene.path, true))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (m != null) lines.Add("USED " + p + " | " + (m.shader == null ? "MISSING" : m.shader.name));
            }
        }
        File.WriteAllLines("Logs/urp-audit.txt", lines);
        Debug.Log("[URP] Audit written to Logs/urp-audit.txt");
    }
}

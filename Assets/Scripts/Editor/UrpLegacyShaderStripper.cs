using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

// Keep PPSv1 assets available for the maintenance converter without shipping their unused shaders.
public sealed class UrpLegacyShaderStripper : IPreprocessShaders
{
    public int callbackOrder { get { return 0; } }

    public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
    {
        if (GraphicsSettings.defaultRenderPipeline != null && AssetDatabase.GetAssetPath(shader)
            .StartsWith("Assets/PostProcessing/Resources/", StringComparison.Ordinal))
            data.Clear();
    }
}

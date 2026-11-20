using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Authors the reusable uGUI settings prefab from the existing menu artwork and font.</summary>
public static class SettingsMenuPrefab
{
    [MenuItem("The Nightmare/UI/Rebuild settings prefab")]
    public static void Build()
    {
        // Match Unity's prefab-stage isolation without changing the active scene. An additive
        // untitled scene cannot be opened when the user already has an unsaved untitled scene.
        const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var type = typeof(EditorSceneManager);
        var getTarget = type.GetMethod("GetTargetSceneForNewGameObjects", flags);
        var setTarget = type.GetMethod("SetTargetSceneForNewGameObjects", flags, null, new[] { typeof(Scene) }, null);
        var clearTarget = type.GetMethod("ClearTargetSceneForNewGameObjects", flags);
        var previous = (Scene)getTarget.Invoke(null, null);
        var temporary = EditorSceneManager.NewPreviewScene();
        try
        {
            setTarget.Invoke(null, new object[] { temporary });
            var go = new GameObject("SettingsPanel", typeof(RectTransform), typeof(SettingsMenuView));
            go.GetComponent<SettingsMenuView>().Build(
                AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/Nevermore SDF.asset"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/main_menu/BG.png"),
                AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Textures/main_menu/return.png"));
            PrefabUtility.SaveAsPrefabAsset(go, "Assets/Resources/UI/SettingsPanel.prefab");
        }
        finally
        {
            if (previous.IsValid()) setTarget.Invoke(null, new object[] { previous });
            else clearTarget.Invoke(null, null);
            EditorSceneManager.ClosePreviewScene(temporary);
        }
    }
}

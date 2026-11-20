using System;
using System.Collections.Generic;
using System.Linq;
using Nightmare.Configuration;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Validates authored campaign, content, scene identities and player bindings before builds.</summary>
public static class ContentValidation
{
    private const string GameScene = "Assets/Scenes/ExtremeHomeMakeover.unity";

    [MenuItem("The Nightmare/Validation/Assign stable scene IDs")]
    public static void AssignStableIds()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        var ids = new HashSet<string>();
        int changed = 0;
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!(component is GridPoint) && !(component is CaptureArea)) continue;
            var serialized = new SerializedObject(component);
            var field = serialized.FindProperty("_stableId");
            if (!string.IsNullOrWhiteSpace(field.stringValue) && ids.Add(field.stringValue)) continue;
            Undo.RecordObject(component, "Assign stable scene ID");
            field.stringValue = Guid.NewGuid().ToString("N");
            ids.Add(field.stringValue);
            serialized.ApplyModifiedProperties();
            changed++;
        }
        if (changed > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"Assigned {changed} stable IDs. Save the scene to persist them.");
    }

    [MenuItem("The Nightmare/Validation/Validate game content")]
    public static void ValidateBuildContent()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode first.");
        Scene scene = SceneManager.GetSceneByPath(GameScene);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(GameScene, OpenSceneMode.Additive);
        try
        {
            var roots = scene.GetRootGameObjects();
            var game = roots.SelectMany(root => root.GetComponentsInChildren<GameManager>(true)).Single();
            var fields = new SerializedObject(game);
            ValidateCampaign(fields);
            ValidateCatalog(fields);
            ValidateSceneObjects(roots);
            ValidatePlayerRigs();
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    private static void ValidateCampaign(SerializedObject fields)
    {
        var campaign = (CampaignDefinition)fields.FindProperty("_campaign").objectReferenceValue;
        Require(campaign != null && campaign.Stages.Length > 0, "Campaign is missing or empty.");
        var bindings = fields.FindProperty("_stageBindings");
        Require(bindings.arraySize == campaign.Stages.Length, "Campaign definitions and scene bindings have different lengths.");
        var stageIds = new HashSet<string>();
        for (int i = 0; i < campaign.Stages.Length; i++)
        {
            var stage = campaign.Stages[i];
            Require(stage != null && !string.IsNullOrWhiteSpace(stage.Id) && stageIds.Add(stage.Id), $"Stage {i} has no unique ID.");
            Require(stage.PreparationSeconds > 0f && stage.DefenseSeconds > 0f, stage.name + " has invalid durations.");
            Require(stage.Enemies != null && stage.Enemies.Any(entry => entry.Prefab != null && entry.Weight > 0f), stage.name + " has no spawnable enemies.");
            Require(bindings.GetArrayElementAtIndex(i).FindPropertyRelative("Room").objectReferenceValue != null, stage.name + " has no scene room.");
        }
    }

    private static void ValidateCatalog(SerializedObject fields)
    {
        var catalog = (ContentCatalog)fields.FindProperty("_content").objectReferenceValue;
        Require(catalog != null && catalog.Weapons.Length > 0, "Content catalog is missing.");
        for (int i = 0; i < catalog.Weapons.Length; i++)
        {
            var weapon = catalog.Weapons[i];
            Require(weapon != null && weapon.LegacyIndex == i && weapon.Prefab != null, $"Weapon slot {i} is invalid.");
            Require(weapon.MagazineCapacity > 0 && weapon.MaximumAmmo >= weapon.MagazineCapacity, weapon.name + " has invalid ammunition.");
        }
        foreach (var item in catalog.Placeables)
            Require(item != null && item.Prefab != null && item.PreviewPrefab != null, "Placeable has no prefab or preview.");
    }

    private static void ValidateSceneObjects(GameObject[] roots)
    {
        var ids = new HashSet<string>();
        foreach (var root in roots)
        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
        {
            Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) == 0, "Missing script on " + transform.name);
            var point = transform.GetComponent<GridPoint>();
            var area = transform.GetComponent<CaptureArea>();
            if (point == null && area == null) continue;
            string id = new SerializedObject((UnityEngine.Object)point ?? area).FindProperty("_stableId").stringValue;
            Require(!string.IsNullOrWhiteSpace(id) && ids.Add(id), "Missing/duplicate scene ID on " + transform.name);
        }
    }

    private static void ValidatePlayerRigs()
    {
        foreach (string name in new[] { "Boy", "Girl" })
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/" + name + ".prefab");
            var rig = player.GetComponent<PlayerRigBindings>();
            Require(rig != null && rig.ActiveAnimator != null && rig.RemoteWeaponHand != null && rig.WeaponHolder != null, name + " has incomplete rig bindings.");
            Require(player.GetComponent<MirrorPlayerCombat>() != null && player.GetComponent<MirrorPlacement>() != null, name + " has no network adapters.");
        }
    }

    private static void Require(bool valid, string message) { if (!valid) throw new InvalidOperationException(message); }
}

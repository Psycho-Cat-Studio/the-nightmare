using UnityEngine;

/// <summary>Applies a Unity rendering/collision layer throughout one object hierarchy.</summary>
public static class TransformLayers
{
    public static void SetRecursively(GameObject root, int layer)
    {
        if (root == null) return;
        root.layer = layer;
        foreach (Transform child in root.transform) SetRecursively(child.gameObject, layer);
    }
    public static void SetRecursively(GameObject root, string layer) => SetRecursively(root, LayerMask.NameToLayer(layer));
}

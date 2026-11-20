using UnityEngine;

/// <summary>Removes BoxColliders recursively during legacy edit-mode mesh authoring.</summary>
[ExecuteInEditMode]
public class MeshColliderRemover : MonoBehaviour
{
    void Awake()
    {
        RemoveMeshColliderRecursively(transform);
    }
    void RemoveMeshColliderRecursively(Transform obj)
    {
        if (obj == null) return;
        if (obj.GetComponent<BoxCollider>() != null) DestroyImmediate(obj.gameObject.GetComponent<BoxCollider>());
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            RemoveMeshColliderRecursively(child);
        }
    }
}

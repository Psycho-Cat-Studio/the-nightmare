using UnityEngine;

/// <summary>Adds BoxColliders to rendered mesh objects during legacy edit-mode authoring.</summary>
[ExecuteInEditMode]
public class MeshColliderGenerator : MonoBehaviour
{
    void Awake()
    {
        AddMeshColliderRecursively(transform);
    }
    void AddMeshColliderRecursively(Transform obj)
    {
        if (obj == null) return;
        if (obj.GetComponent<MeshRenderer>() != null) obj.gameObject.AddComponent<BoxCollider>();
        foreach (Transform child in obj.transform)
        {
            if (child == null) continue;
            AddMeshColliderRecursively(child);
        }
    }

}

using UnityEngine;

/// <summary>Removes unbound legacy grid points during edit-mode authoring.</summary>
[ExecuteInEditMode]
public class DeletePointsWithoutRenderer : MonoBehaviour
{
    void Awake()
    {
        foreach (Transform child in transform)
        {
            GridPoint gridPoint = child.GetComponent<GridPoint>();
            if (gridPoint.getSpriteRenderer() == null) DestroyImmediate(child.gameObject); 
        }
    }
}

using UnityEngine;

namespace Nightmare.Configuration
{
    /// <summary>Authors a placeable prefab, preview model and inventory capacity.</summary>
    [CreateAssetMenu(menuName = "The Nightmare/Placeable")]
    public sealed class PlaceableDefinition : ScriptableObject
    {
        public string Id;
        public GameObject Prefab;
        public GameObject PreviewPrefab;
        [Min(0)] public int InventoryLimit;
    }
}

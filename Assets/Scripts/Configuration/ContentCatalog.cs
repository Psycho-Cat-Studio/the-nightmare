using UnityEngine;

namespace Nightmare.Configuration
{
    /// <summary>Groups the weapon and placeable definitions referenced by scene composition and local loadouts.</summary>
    [CreateAssetMenu(menuName = "The Nightmare/Content Catalog")]
    public sealed class ContentCatalog : ScriptableObject
    {
        public WeaponDefinition[] Weapons;
        public PlaceableDefinition[] Placeables;
    }
}

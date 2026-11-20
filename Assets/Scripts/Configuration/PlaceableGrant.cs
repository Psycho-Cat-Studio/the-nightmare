using UnityEngine;

namespace Nightmare.Configuration
{
    /// <summary>Pairs a placeable definition with the amount granted by a chest.</summary>
    [System.Serializable]
    public sealed class PlaceableGrant
    {
        public PlaceableDefinition Item;
        [Min(0)] public int Amount;
    }
}

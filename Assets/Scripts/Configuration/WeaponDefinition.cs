using UnityEngine;

namespace Nightmare.Configuration
{
    public enum WeaponFireMode { Single, Burst, Automatic }
    /// <summary>Authors weapon combat parameters, allowed fire modes, catalog identity and view poses.</summary>
    [CreateAssetMenu(menuName = "The Nightmare/Weapon")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        public string Id;
        /// <summary>Stable zero-based catalog index used by existing pickups and weapon replication.</summary>
        public int LegacyIndex;
        public string DisplayName;
        public GameObject Prefab;
        [Min(1)] public int Damage;
        [Min(1)] public int MagazineCapacity;
        /// <summary>Maximum total ammunition, including loaded rounds.</summary>
        [Min(1)] public int MaximumAmmo;
        [Min(0f)] public float Range;
        [Min(0f)] public float FireInterval;
        [Min(0f)] public float ReloadSeconds;
        [Min(0f)] public float Recoil;
        public bool AllowBurst;
        public bool AllowAutomatic;
        public WeaponFireMode DefaultMode;
        public Vector3 FirstPersonPosition;
        public Vector3 RemotePosition;
        public Vector3 RemoteEuler;
    }
}

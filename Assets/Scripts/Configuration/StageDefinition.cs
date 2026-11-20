using UnityEngine;

namespace Nightmare.Configuration
{
    /// <summary>Associates an enemy prefab with a relative stage-spawn weight.</summary>
    [System.Serializable]
    public sealed class EnemySpawnEntry
    {
        public GameObject Prefab;
        [Min(0f)] public float Weight = 1f;
    }

    /// <summary>Authors one preparation/defense stage and its weighted enemy selection.</summary>
    [CreateAssetMenu(menuName = "The Nightmare/Stage")]
    public sealed class StageDefinition : ScriptableObject
    {
        public string Id;
        [Min(0f)] public float PreparationSeconds = 60f;
        [Min(0f)] public float DefenseSeconds = 120f;
        public EnemySpawnEntry[] Enemies;

        /// <summary>Selects a prefab by nonnegative authored weights and a sample clamped to zero through one.</summary>
        /// <returns>A weighted eligible prefab, or null if no entry is eligible.</returns>
        public GameObject SelectEnemy(float sample)
        {
            float sum = 0f;
            foreach (var entry in Enemies) if (entry.Prefab != null) sum += Mathf.Max(0f, entry.Weight);
            float pick = Mathf.Clamp01(sample) * sum;
            GameObject last = null;
            foreach (var entry in Enemies)
            {
                if (entry.Prefab == null || entry.Weight <= 0f) continue;
                last = entry.Prefab;
                pick -= entry.Weight;
                if (pick < 0f) return last;
            }
            return last;
        }
    }
}

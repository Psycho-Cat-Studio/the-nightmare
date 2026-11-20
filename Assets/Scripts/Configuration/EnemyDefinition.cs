using UnityEngine;

namespace Nightmare.Configuration
{
    /// <summary>Authors enemy health, movement, damage, death timing and susceptibility to bricks.</summary>
    [CreateAssetMenu(menuName = "The Nightmare/Enemy")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        public string Id;
        [Min(1f)] public float Health;
        [Min(0f)] public float WalkSpeed;
        [Min(0f)] public float RunSpeed;
        [Min(0f)] public float DamagePerSecond;
        [Min(0f)] public float DeathAnimationSeconds;
        public bool AffectedByBricks;
    }
}

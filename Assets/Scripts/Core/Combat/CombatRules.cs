namespace Nightmare.Core.Combat
{
    /// <summary>Shares authored hit multipliers and scream timing between combat decisions and adapters.</summary>
    public static class CombatRules
    {
        // Shared by hit reporting and server validation; preserves the authored damage cap.
        public const float HeadDamageMultiplier = 3f;
        public const float BodyDamageMultiplier = 2f;
        public const float LegDamageMultiplier = 1f;
        public const float MaximumDamageMultiplier = HeadDamageMultiplier;
        public const float InitialScreamDelaySeconds = 1f;
        public const float ScreamDurationSeconds = 2.2f;
    }
}

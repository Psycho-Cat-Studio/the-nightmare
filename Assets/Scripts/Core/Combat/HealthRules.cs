using System;

namespace Nightmare.Core.Combat
{
    /// <summary>Computes permitted finite damage without allowing health to fall below zero.</summary>
    public static class HealthRules
    {
        /// <summary>Applies allowed, positive finite damage and floors health at zero; rejected damage leaves health unchanged.</summary>
        public static float ApplyDamage(float current, float damage, bool allowed)
        {
            if (!allowed || damage <= 0f || float.IsNaN(damage) || float.IsInfinity(damage)) return current;
            return Math.Max(0f, current - damage);
        }
    }
}

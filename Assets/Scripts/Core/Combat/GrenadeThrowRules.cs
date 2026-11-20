using System;

namespace Nightmare.Core.Combat
{
    /// <summary>Shared charge-to-force conversion used by the owner and server validation.</summary>
    public static class GrenadeThrowRules
    {
        public const float MaximumChargeSeconds = 3f;
        private const float InitialChargeSeconds = 0.5f;
        private const float NormalizationSeconds = 3f;
        public const float ForwardSpeed = 15f;
        public const float UpwardSpeed = 5f;
        public static float MaximumForce => ForceFromCharge(MaximumChargeSeconds);

        /// <summary>Converts seconds to force without clamping the owner's final-frame charge overshoot.</summary>
        public static float ForceFromCharge(float seconds) => (seconds + InitialChargeSeconds) / NormalizationSeconds;

        /// <summary>Clamps a reported force to the same maximum as the original server rule.</summary>
        public static float ClampReportedForce(float force) => Math.Max(0f, Math.Min(MaximumForce, force));
    }
}

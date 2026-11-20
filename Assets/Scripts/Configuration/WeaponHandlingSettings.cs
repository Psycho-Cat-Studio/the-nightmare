using UnityEngine;

namespace Nightmare.Configuration
{
    /// <summary>Local weapon handling and feedback tuning; defaults preserve the authored feel.</summary>
    [System.Serializable]
    public sealed class WeaponHandlingSettings
    {
        [Min(0f)] public float SwapHalfSeconds = 0.5f;
        [Min(0f)] public float SwapCooldownSeconds = 2f;
        [Min(0f)] public float HitmarkerSeconds = 0.15f;
        [Min(0f)] public float AdsRecoilMultiplier = 0.35f;
        [Min(0f)] public float AdsDamping = 16.643f;
        [Min(0f)] public float CrosshairRestThreshold = 1.02f;
        public float CrosshairRecoveryBias = 0.4f;
        [Range(0f, 1f)] public float CrosshairRecoveryPerFrame = 0.95f;
        [Min(1f)] public float ReferenceFrameRate = 50f;
    }
}

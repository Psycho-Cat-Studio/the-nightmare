using System;

namespace Nightmare.Core.Match
{
    /// <summary>Owns remaining phase time independently of HUD updates and Unity frame callbacks.</summary>
    public sealed class PhaseClock
    {
        /// <summary>Remaining seconds; may become negative until the match resolves the expired phase.</summary>
        public float Remaining { get; set; }
        /// <summary>Whether Tick is allowed to advance this session-owned clock.</summary>
        public bool IsRunning { get; set; }
        /// <summary>Subtracts nonnegative elapsed seconds while running; it does not resolve phase transitions.</summary>
        public void Tick(float deltaTime) { if (IsRunning) Remaining -= Math.Max(0f, deltaTime); }
    }
}

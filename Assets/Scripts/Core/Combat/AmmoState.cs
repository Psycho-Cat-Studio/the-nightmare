using System;

namespace Nightmare.Core.Combat
{
    /// <summary>Total includes the loaded magazine. Reloading transfers rounds, never creates them.</summary>
    public sealed class AmmoState
    {
        public int Capacity { get; }
        public int Maximum { get; }
        /// <summary>All remaining rounds, including those already loaded in the magazine.</summary>
        public int Total { get; private set; }
        /// <summary>Loaded rounds available to fire without reloading.</summary>
        public int Magazine { get; private set; }
        /// <summary>Unloaded rounds; always Total minus Magazine.</summary>
        public int Reserve => Total - Magazine;
        /// <summary>Number of complete spare magazines, excluding loaded rounds and any partial spare.</summary>
        public int FullReserveMagazines => Reserve / Capacity;

        /// <summary>Creates ammunition state and clamps supplied counts to its capacities.</summary>
        /// <exception cref="ArgumentOutOfRangeException">Capacity is nonpositive, or maximum is below capacity.</exception>
        public AmmoState(int capacity, int maximum, int total, int magazine)
        {
            if (capacity <= 0 || maximum < capacity) throw new ArgumentOutOfRangeException(nameof(capacity));
            Capacity = capacity;
            Maximum = maximum;
            Restore(total, magazine);
        }

        /// <summary>Restores total and loaded counts while enforcing capacity and nonnegative-count invariants.</summary>
        public void Restore(int total, int magazine)
        {
            Total = Math.Max(0, Math.Min(Maximum, total));
            Magazine = Math.Max(0, Math.Min(Math.Min(Capacity, Total), magazine));
        }

        /// <summary>Spends one loaded round, decreasing both the magazine and total.</summary>
        /// <returns>false when the magazine is empty; otherwise true.</returns>
        public bool TryConsume()
        {
            if (Magazine == 0) return false;
            Magazine--;
            Total--;
            return true;
        }

        /// <summary>Loads up to capacity from existing rounds without changing Total.</summary>
        public void Reload() { Magazine = Math.Min(Capacity, Total); }
        /// <summary>Replenishes Total to Maximum while preserving the current magazine.</summary>
        public void Refill() { Total = Maximum; }
        /// <summary>Restores maximum total ammunition and a full magazine.</summary>
        public void Reset() { Total = Maximum; Magazine = Capacity; }
    }
}

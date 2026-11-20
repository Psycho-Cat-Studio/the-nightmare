using System;

namespace Nightmare.Core.Match
{
    public enum MatchPhase { None, Lobby, Prepare, Defend, Won, Lost }

    public readonly struct MatchProgress : IEquatable<MatchProgress>
    {
        public MatchPhase Phase { get; }
        /// <summary>Zero-based stage index; stage-specific phases are Prepare and Defend.</summary>
        public int StageIndex { get; }
        public bool IsOver => Phase == MatchPhase.Won || Phase == MatchPhase.Lost;
        /// <summary>Whether combat damage is allowed: preparation and defense allow it; lobby and results do not.</summary>
        public bool CanDamagePlayers => Phase == MatchPhase.Prepare || Phase == MatchPhase.Defend;
        public MatchProgress(MatchPhase phase, int stageIndex = 0)
        {
            Phase = phase;
            StageIndex = Math.Max(0, stageIndex);
        }
        /// <summary>Advances preparation to defense, or defense to the next preparation or victory.</summary>
        /// <remarks>Other phases remain unchanged. This computes a value; it does not broadcast or change scene state.</remarks>
        public MatchProgress Next(int stageCount)
        {
            if (Phase == MatchPhase.Prepare) return new MatchProgress(MatchPhase.Defend, StageIndex);
            if (Phase != MatchPhase.Defend) return this;
            return StageIndex + 1 < stageCount ? new MatchProgress(MatchPhase.Prepare, StageIndex + 1)
                : new MatchProgress(MatchPhase.Won, StageIndex);
        }
        public bool Equals(MatchProgress other) => Phase == other.Phase && StageIndex == other.StageIndex;
        public override bool Equals(object other) => other is MatchProgress progress && Equals(progress);
        public override int GetHashCode() => ((int)Phase * 397) ^ StageIndex;
    }
}

using System;
namespace Nightmare.Core.Match
{
    /// <summary>Owns one session's progress, readiness and clock, independently of rendering and transport.</summary>
    public sealed class MatchSession
    {
        private readonly ReadyRoster _ready = new ReadyRoster();
        public MatchProgress Progress { get; private set; } = new MatchProgress(MatchPhase.None);
        public PhaseClock Clock { get; } = new PhaseClock();
        public int ReadyCount => _ready.Count;
        public bool RegisterReady(string id) => Progress.Phase == MatchPhase.Lobby && _ready.Add(id);
        public void RemoveReady(string id) => _ready.Remove(id);
        public bool CanBegin(int playerCount) => Progress.Phase == MatchPhase.Lobby && _ready.CanStart(playerCount);

        /// <summary>Applies an accepted or replicated value and initializes its clock; duplicate notifications do nothing.</summary>
        public bool Apply(MatchProgress progress, float duration)
        {
            if (Progress.Equals(progress)) return false;
            Progress = progress;
            Clock.Remaining = duration;
            Clock.IsRunning = progress.Phase == MatchPhase.Prepare || progress.Phase == MatchPhase.Defend;
            return true;
        }

        /// <summary>Computes the authority's next value, giving current-stage loss priority over an expired clock.</summary>
        public MatchProgress Resolve(bool allPlayersDown, bool currentAreaLost, int stageCount)
        {
            bool combatLost = Progress.CanDamagePlayers && (allPlayersDown ||
                (Progress.Phase == MatchPhase.Defend && currentAreaLost));
            if (combatLost) return new MatchProgress(MatchPhase.Lost, Progress.StageIndex);
            return Clock.IsRunning && Clock.Remaining <= 0f ? Progress.Next(stageCount) : Progress;
        }

        /// <summary>Whether a transition successfully completes the currently defended stage.</summary>
        public bool CompletesStage(MatchProgress next) => Progress.Phase == MatchPhase.Defend &&
            (next.Phase == MatchPhase.Won || (next.Phase == MatchPhase.Prepare && next.StageIndex == Progress.StageIndex + 1));
    }
}

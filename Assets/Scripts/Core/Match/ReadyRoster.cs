using System.Collections.Generic;

namespace Nightmare.Core.Match
{
    /// <summary>Tracks unique ready-player IDs and determines when the connected roster can start.</summary>
    public sealed class ReadyRoster
    {
        private readonly HashSet<string> _ready = new HashSet<string>();
        public int Count => _ready.Count;
        /// <summary>Registers a unique ready player.</summary>
        /// <returns>true for a newly added ID; false for a duplicate.</returns>
        public bool Add(string playerId) => _ready.Add(playerId);
        /// <summary>Withdraws readiness, including when a player disconnects.</summary>
        public bool Remove(string playerId) => _ready.Remove(playerId);
        /// <summary>Reports whether at least one player is connected and the ready count covers all connections.</summary>
        /// <remarks>The caller must remove disconnected IDs and restrict registration to connected players.</remarks>
        public bool CanStart(int connectedPlayers) => connectedPlayers > 0 && Count >= connectedPlayers;
    }
}

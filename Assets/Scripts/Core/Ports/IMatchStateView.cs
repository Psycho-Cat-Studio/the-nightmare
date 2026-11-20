using Nightmare.Core.Match;

namespace Nightmare.Core.Ports
{
    /// <summary>Read-only match rules required by player runtime and presentation.</summary>
    public interface IMatchStateView
    {
        MatchProgress Progress { get; }
        bool CanDamagePlayers { get; }
    }
    public interface IInputGate { bool CanReadInput { get; } }
}

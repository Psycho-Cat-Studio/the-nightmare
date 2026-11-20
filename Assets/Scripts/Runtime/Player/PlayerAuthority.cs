using Nightmare.Core.Ports;
using UnityEngine;

/// <summary>Transport-neutral control context. The network adapter assigns ownership explicitly.</summary>
public sealed class PlayerAuthority : MonoBehaviour, IAuthorityContext
{
    private IInputGate _input;
    public IPlayerOptions Options { get; private set; }
    public void BindOptions(IPlayerOptions options) { Options = options; }
    public IMatchStateView Match { get; private set; }
    public bool CanReadInput => _input != null && _input.CanReadInput;
    public void BindGame(IMatchStateView match, IInputGate input) { Match = match; _input = input; }
    public bool IsSimulationAuthority { get; private set; }
    public bool IsLocalPlayer { get; private set; }
    public void Initialize(bool localPlayer, bool simulationAuthority)
    {
        IsLocalPlayer = localPlayer;
        IsSimulationAuthority = simulationAuthority;
    }
}

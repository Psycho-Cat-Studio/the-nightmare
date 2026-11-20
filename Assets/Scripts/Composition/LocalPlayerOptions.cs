using Nightmare.Core.Ports;

/// <summary>Adapts persisted local preferences to the player-runtime options contract.</summary>
public sealed class LocalPlayerOptions : IPlayerOptions
{
    public float MouseSensitivity => GameSettings.MouseSensitivity;
    public bool InvertY => GameSettings.InvertY;
}

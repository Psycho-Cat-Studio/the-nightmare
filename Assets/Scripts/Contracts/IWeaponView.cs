using UnityEngine;

public interface IWeaponView { GameObject ActiveWeapon { get; } }
public interface IPlayerLoadout : IWeaponView { bool IsSwitchingWeapon { get; } }
public interface IAnalogPlayerInput
{
    Vector2 Movement { get; }
    Vector2 Look { get; }
}

/// <summary>Mutable input snapshot for event-driven input adapters and deterministic simulations.</summary>
public sealed class AnalogInputState : IAnalogPlayerInput
{
    public Vector2 Movement { get; set; }
    public Vector2 Look { get; set; }
}

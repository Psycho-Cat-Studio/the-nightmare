using UnityEngine;

/// <summary>Adapts the existing movement and look joysticks to analog player input.</summary>
public sealed class MobilePlayerInput : IAnalogPlayerInput
{
    private readonly Joystick _movement;
    private readonly Joystick _look;
    public MobilePlayerInput(MonoBehaviour movement, MonoBehaviour look)
    {
        _movement = movement as Joystick;
        _look = look as Joystick;
        if (_movement == null) _movement = GameObject.Find("Move")?.GetComponent<Joystick>();
        if (_look == null) _look = GameObject.Find("Look")?.GetComponent<Joystick>();
    }
    public Vector2 Movement => _movement != null ? new Vector2(_movement.Horizontal, _movement.Vertical) : Vector2.zero;
    public Vector2 Look => _look != null ? new Vector2(_look.Horizontal, _look.Vertical) : Vector2.zero;
}

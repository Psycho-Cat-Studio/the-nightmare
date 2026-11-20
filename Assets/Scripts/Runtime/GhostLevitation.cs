using System;
using UnityEngine;

/// <summary>Adds bounded, seedable model motion above the Animator without moving the navigation root or consuming gameplay randomness.</summary>
public sealed class GhostLevitation : MonoBehaviour
{
    [SerializeField, Min(0f), Tooltip("Peak-to-peak upward travel in world meters; never lowers the authored pivot.")]
    private float _verticalTravel = 0.08f;
    [SerializeField, Min(0f), Tooltip("Maximum sideways displacement in world meters.")]
    private float _lateralAmplitude = 0.015f;
    [SerializeField, Min(0f)] private float _rollDegrees = 2f;
    [SerializeField, Min(0f)] private float _pitchDegrees = 1f;
    [SerializeField, Min(0f)] private float _frequencyHz = 0.5f;
    private const double SeedSpacing = 0.6180339887498949;
    private Transform _pivot;
    private Transform _parent;
    private Vector3 _restPosition;
    private Quaternion _restRotation;
    private bool _frozen;

    private void Awake()
    {
        _pivot = transform;
        _parent = _pivot.parent;
        _restPosition = _pivot.localPosition;
        _restRotation = _pivot.localRotation;
    }

    /// <summary>Samples absolute motion from shared seconds and a stable entity seed; repeated samples never accumulate offsets.</summary>
    /// <remarks>The transport adapter supplies time/identity. This component has no networking dependency or frame callback.</remarks>
    public void Present(double time, uint seed)
    {
        if (_frozen || !isActiveAndEnabled) return;
        double phase = (time * _frequencyHz + seed * SeedSpacing) % 1.0 * (2.0 * Math.PI);
        float sin = (float)Math.Sin(phase);
        float cos = (float)Math.Cos(phase);
        var right = _parent != null ? _parent.right : Vector3.right;
        var worldOffset = Vector3.up * (_verticalTravel * 0.5f * (1f + sin)) + right * (_lateralAmplitude * cos);
        var localOffset = _parent != null ? _parent.InverseTransformVector(worldOffset) : worldOffset;
        _pivot.localPosition = _restPosition + localOffset;
        _pivot.localRotation = _restRotation * Quaternion.Euler(_pitchDegrees * sin, 0f, -_rollDegrees * cos);
    }

    /// <summary>Holds the current visual pose for combat death or stage retirement; it does not snap back before dissolve.</summary>
    public void Freeze() { _frozen = true; }

    private void OnDisable()
    {
        if (_pivot == null) return;
        _pivot.localPosition = _restPosition;
        _pivot.localRotation = _restRotation;
        _frozen = false;
    }
}

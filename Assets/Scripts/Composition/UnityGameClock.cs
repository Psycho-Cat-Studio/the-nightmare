using Nightmare.Core.Ports;
using UnityEngine;

/// <summary>Supplies Unity frame elapsed time to engine-independent match timing.</summary>
public sealed class UnityGameClock : IClock
{
    public float DeltaTime => Time.deltaTime;
}

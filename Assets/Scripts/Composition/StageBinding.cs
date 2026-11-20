using System;
using UnityEngine;

/// <summary>Scene references stay in the scene, not in shared configuration assets.</summary>
[Serializable]
public sealed class StageBinding
{
    public Room Room;
    public GameObject Chest;
    public GameObject EntryDoor;
}

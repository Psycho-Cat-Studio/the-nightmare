using UnityEngine;

public interface IEnemyPerception
{
    bool IsAuthority { get; }
    /// <summary>Whether perception may currently accept a player, considering authority, initialization and enemy state.</summary>
    bool CanDetectPlayer { get; }
    void DetectPlayer(Transform player);
}
public interface IEnemyTrapReceiver
{
    /// <summary>Attempts an authoritative brick freeze.</summary>
    /// <returns>true only when the freeze was applied; only then should the caller consume a trap charge.</returns>
    bool TryFreeze(float seconds);
}
public interface ITrapConsumable
{
    float FreezeSeconds { get; }
    /// <summary>Consumes a trap charge after an accepted freeze; may release its cell and despawn the trap.</summary>
    void RegisterUse();
}
public interface IEntityLifetime
{
    bool IsAuthority { get; }
    /// <summary>Requests authority-side removal; non-authoritative presentation must not remove the entity independently.</summary>
    void Despawn();
}

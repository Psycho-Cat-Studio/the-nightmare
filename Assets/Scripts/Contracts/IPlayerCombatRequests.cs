using UnityEngine;

/// <summary>Gameplay requests, independent of a networking library. A backend adapter supplies delivery.</summary>
public interface IPlayerCombatRequests
{
    /// <summary>Notifies peers of shot presentation; ammunition has already been spent by the owner.</summary>
    void Shot();
    void EnvironmentHit(Vector3 point, Vector3 normal);
    /// <summary>Reports an enemy hit for authoritative validation and damage.</summary>
    /// <param name="id">Replicated target identity.</param>
    /// <param name="damage">Requested damage including the hit-region multiplier.</param>
    /// <param name="localPoint">Impact point in the target root local space.</param>
    /// <param name="localNormal">Impact normal in the target root local space.</param>
    void EnemyHit(string id, float damage, Vector3 localPoint, Vector3 localNormal);
    /// <summary>Reports player damage for authoritative life, phase and damage-bound checks.</summary>
    void PlayerHit(string id, float damage);
    void ExplodeBarrel(uint id);
    /// <summary>Requests a grenade launch using owner aim and charge-derived force; the server bounds force before spawning.</summary>
    void ThrowGrenade(Vector3 position, Quaternion rotation, Vector3 direction, float force);
}

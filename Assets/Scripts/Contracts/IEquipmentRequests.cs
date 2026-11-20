using UnityEngine;

public interface IEquipmentRequests
{
    /// <summary>Requests authoritative revival of the identified teammate.</summary>
    void Revive(string playerId);
    void RemoveChairs(Vector3 position);
    /// <summary>Requests replication of the held weapon catalog ID; local loadout changes happen separately.</summary>
    void SetActiveWeapon(int weaponId);
}

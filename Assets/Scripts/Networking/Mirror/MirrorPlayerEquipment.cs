using Mirror;
using UnityEngine;

/// <summary>Transports weapon-selection, revive and removable-chair requests and their replicated outcomes.</summary>
[RequireComponent(typeof(PlayerEquipment))]
public sealed class MirrorPlayerEquipment : NetworkBehaviour, IEquipmentRequests, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    private PlayerEquipment _equipment;
    void Awake() { _equipment = GetComponent<PlayerEquipment>(); _equipment.InitializeEquipment(this); }
    public void Revive(string playerId) => CmdCallRevive(playerId);
    public void RemoveChairs(Vector3 position) => CmdRemoveChairs(position);
    public void SetActiveWeapon(int weaponId) => CmdSetActiveWeapon(weaponId);
    [ClientRpc] void RpcSetActiveWeapon(int weaponId) { _equipment.ShowRemoteWeapon(weaponId); }
    [Command]
    private void CmdCallRevive(string name)
    {
        PlayerManager target = _game.Players.GetPlayer(name);
        PlayerManager self = GetComponent<PlayerManager>();
        if (target == null || !target.IsDead || self.IsDead) return;
        if (Vector3.Distance(target.transform.position, transform.position) > 3f) return;
        target.Revive();
        RpcCallRevive(name);
    }

    // Removable chairs are scene objects without a NetworkIdentity, so they are found by position.
    [Command]
    private void CmdRemoveChairs(Vector3 position)
    {
        RpcRemoveChairs(position);
    }

    [ClientRpc]
    private void RpcRemoveChairs(Vector3 position)
    {
        foreach (GameObject chairs in GameObject.FindGameObjectsWithTag(GameTags.RemovableChairs))
        {
            if ((chairs.transform.position - position).sqrMagnitude < 0.01f)
            {
                Destroy(chairs);
                return;
            }
        }
    }

    [ClientRpc]
    private void RpcCallRevive(string name)
    {
        PlayerManager target = _game.Players.GetPlayer(name);
        if (!isServer && target != null) target.Revive();
    }
    
    [Command]
    public void CmdSetActiveWeapon(int weaponId) {
        if (weaponId < 0 || weaponId >= _game.Weapons.Length) return;
        RpcSetActiveWeapon(weaponId);
    }

}

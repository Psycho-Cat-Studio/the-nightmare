using Mirror;
using Nightmare.Core.Combat;
using UnityEngine;

// Owns transport and server validation; local input, ammo and first-person presentation live elsewhere.
/// <summary>Transports shot/hit/grenade requests and applies server target and damage-bound validation.</summary>
[RequireComponent(typeof(PlayerShoot))]
public sealed class MirrorPlayerCombat : NetworkBehaviour, IPlayerCombatRequests, IMatchParticipant
{
    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    private PlayerEquipment Equipment;
    private GameObject _grenadePrefab;
    void Awake()
    {
        Equipment = GetComponent<PlayerEquipment>();
        var shoot = GetComponent<PlayerShoot>();
        _grenadePrefab = shoot.GrenadePrefab;
        shoot.InitializeCombat(this);
    }
    public void Shot() => CmdPlayerShooting();
    public void EnvironmentHit(Vector3 point, Vector3 normal) => CmdOnHit(point, normal);
    public void EnemyHit(string id, float damage, Vector3 localPoint, Vector3 localNormal) => CmdEnemyShoot(id, damage, localPoint, localNormal);
    public void PlayerHit(string id, float damage) => CmdPlayerShoot(id, damage);
    public void ExplodeBarrel(uint id) => CmdExplodeBarrel(id);
    public void ThrowGrenade(Vector3 position, Quaternion rotation, Vector3 direction, float force) => CmdSpawnGrenade(position, rotation, direction, force);
    [ClientRpc] void RpcShot() { Equipment.PlayRemoteShot(); }
    [ClientRpc] void RpcImpact(Vector3 point, Vector3 normal) { Equipment.PlayRemoteImpact(point, normal); }
    [Command]
    public void CmdExplodeBarrel(uint objectId)
    {
        // Another shot or a chain reaction may already have destroyed it.
        NetworkIdentity barrelIdentity;
        if (!NetworkServer.spawned.TryGetValue(objectId, out barrelIdentity) || barrelIdentity == null) return;
        Barrel barrel = barrelIdentity.GetComponent<Barrel>();
        if (barrel != null) barrel.Explode();
    }

    [Command]
    void CmdPlayerShooting() {
        RpcShot();
    }

    [Command]
    void CmdOnHit(Vector3 hitPoint, Vector3 normal) {
        RpcImpact(hitPoint, normal);
    }

    // The client reports hits (no lag compensation yet), so at least reject impossible damage.
    [Server]
    private bool IsPlausibleDamage(float damage) {
        PlayerManager self = GetComponent<PlayerManager>();
        if (self == null || self.IsDead || damage <= 0f) return false;
        float maxDamage = 0f;
        foreach (GameObject weaponPrefab in _game.Weapons) {
            Weapon weapon = weaponPrefab != null ? weaponPrefab.GetComponent<Weapon>() : null;
            if (weapon != null) maxDamage = Mathf.Max(maxDamage, weapon.Definition != null ? weapon.Definition.Damage : weapon.Damage);
        }
        return damage <= maxDamage * Nightmare.Core.Combat.CombatRules.MaximumDamageMultiplier;
    }

    [Command]
    void CmdEnemyShoot(string shootEnemyId, float damage, Vector3 localPoint, Vector3 localNormal) {
        EnemyControllerServer enemy = _game.World.GetEnemy(shootEnemyId) as EnemyControllerServer;
        if (enemy == null || enemy._isDying || !IsPlausibleDamage(damage)) return;
        var feedback = enemy.GetComponent<EnemyFeedback>();
        // Impact validation is cosmetic; it does not replace the existing damage validation.
        bool validImpact = feedback != null && feedback.IsValidImpact(localPoint, localNormal);
        enemy.TakeDamage(damage);
        if (validImpact) feedback.BulletHit(localPoint, localNormal);
    }

    [Command]
    void CmdPlayerShoot(string shootPlayerId, float damage) {
        PlayerManager target = _game.Players.GetPlayer(shootPlayerId);
        if (target != null && IsPlausibleDamage(damage)) target.TakeDamage(damage);
    }

    [Command]
    void CmdSpawnGrenade(Vector3 playerPos, Quaternion playerRot, Vector3 aimDirection, float force) {
        // Charge is (0.5..3.5)/3 on the client; clamp what a client may send.
        force = GrenadeThrowRules.ClampReportedForce(force);
        aimDirection = aimDirection.sqrMagnitude > 0.0001f ? aimDirection.normalized : transform.forward;
        // Spawn in front of the body at chest height, independent of the aim pitch, so it never starts in the floor.
        Vector3 flatForward = Vector3.ProjectOnPlane(aimDirection, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f) flatForward = transform.forward;
        GameObject grenade = Instantiate(_grenadePrefab, playerPos + flatForward.normalized + Vector3.up / 2, playerRot);
        Rigidbody rb = grenade.GetComponent<Rigidbody>();
        rb.linearVelocity = force * (GrenadeThrowRules.ForwardSpeed * aimDirection + GrenadeThrowRules.UpwardSpeed * Vector3.up);
        NetworkServer.Spawn(grenade);
    }
}

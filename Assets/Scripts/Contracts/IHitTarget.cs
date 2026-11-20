using UnityEngine;

public interface IHitTarget
{
    string EntityId { get; }
    Transform Root { get; }
}
public interface IEnemyHitTarget : IHitTarget { bool UsesConfirmedHitEffects { get; } }
public interface IPlayerHitTarget : IHitTarget { bool IsDead { get; } }
public interface IExplosiveHitTarget { uint EntityId { get; } }

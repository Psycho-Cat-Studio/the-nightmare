/// <summary>Authority-owned enemy lifetime at a successful stage boundary, separate from combat death.</summary>
public interface IStageEnemy : IEnemyHitTarget
{
    /// <summary>Immediately ends combat/collision participation and publishes one cosmetic dissolve before server despawn.</summary>
    void RetireForStageCompletion();
}

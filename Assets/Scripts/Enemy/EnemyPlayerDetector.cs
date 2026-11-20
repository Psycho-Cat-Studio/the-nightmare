using UnityEngine;

/// <summary>Forwards eligible player detection to the owning enemy simulation.</summary>
public class EnemyPlayerDetector : MonoBehaviour
{
    private IEnemyPerception _receiver;
    void Awake() { _receiver = GetComponentInParent<IEnemyPerception>(); }
    void Start() { if (_receiver == null || !_receiver.IsAuthority) enabled = false; }
    void OnTriggerEnter(Collider other)
    {
        if (!enabled || _receiver == null || !_receiver.CanDetectPlayer || !other.CompareTag(GameTags.Player)) return;
        var player = other.GetComponentInParent<IPlayerHitTarget>();
        if (player != null && !player.IsDead) _receiver.DetectPlayer(player.Root);
    }
}

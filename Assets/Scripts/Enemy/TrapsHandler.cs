using UnityEngine;

/// <summary>Connects enemy trap contact to authoritative freezing and consumes charges only for accepted freezes.</summary>
public class TrapsHandler : MonoBehaviour
{
    private IEnemyTrapReceiver _receiver;
    void Awake() { _receiver = GetComponentInParent<IEnemyTrapReceiver>(); }
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(GameTags.Snares) || _receiver == null) return;
        var trap = other.GetComponent<ITrapConsumable>();
        if (trap != null && _receiver.TryFreeze(trap.FreezeSeconds)) trap.RegisterUse();
    }
}

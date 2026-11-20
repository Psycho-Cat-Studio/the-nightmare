using UnityEngine;

/// <summary>Reports melee contact boundaries to the authoritative teddy-bear fight state.</summary>
public class TeddyBearCollider : MonoBehaviour
{
    void Start()
    {
        if (GetComponentInParent<IEntityLifetime>()?.IsAuthority != true) enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag(GameTags.EnemyLegs) || other.CompareTag(GameTags.EnemyHead) || other.CompareTag(GameTags.EnemyBody))
            transform.parent.GetComponent<TeddyBearServer>().DamageEntryDetected(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if(other.CompareTag(GameTags.EnemyLegs) || other.CompareTag(GameTags.EnemyHead) || other.CompareTag(GameTags.EnemyBody))
            transform.parent.GetComponent<TeddyBearServer>().DamageExitDetected(other);
    }
}

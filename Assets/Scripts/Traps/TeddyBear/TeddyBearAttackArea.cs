using System;
using UnityEngine;

/// <summary>Reports nearby enemy entry and exit to the authoritative teddy-bear target collection.</summary>
public class TeddyBearAttackArea : MonoBehaviour
{
    void Start()
    {
        if (GetComponentInParent<IEntityLifetime>()?.IsAuthority != true) enabled = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag(GameTags.EnemyLegs))
            transform.parent.GetComponent<TeddyBearServer>().EntryDetected(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if(other.CompareTag(GameTags.EnemyLegs))
            transform.parent.GetComponent<TeddyBearServer>().ExitDetected(other);
    }
}

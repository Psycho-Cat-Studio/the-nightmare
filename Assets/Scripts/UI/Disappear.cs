using System.Collections;
using UnityEngine;

/// <summary>Removes a transient object after its authored lifetime.</summary>
public class Disappear : MonoBehaviour
{
    public float time;
    void Start()
    {
        StartCoroutine(Fade());
    }

    private IEnumerator Fade()
    {
        yield return new WaitForSeconds(time);
        Destroy(gameObject);
    }
}

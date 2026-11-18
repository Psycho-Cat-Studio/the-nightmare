using System.Collections;
using UnityEngine;
using Mirror;

public class Barrel : NetworkBehaviour
{
    [SerializeField]
    private GameObject _explosionPrefab;
    private MeshRenderer _renderer;
    private BoxCollider _collider;
    public float damage;
    public string InitialPosAndTag { get; set; }
    private bool _exploded;

    private void Awake()
    {
        _collider = GetComponent<BoxCollider>();
        _renderer = GetComponent<MeshRenderer>();
    }

    // Server only. Two shots, a grenade and a neighbouring barrel can all trigger it in the same
    // frame; it used to explode (and be NetworkServer.Destroy-ed) once per trigger.
    [Server]
    public void Explode()
    {
        if (_exploded) return;
        _exploded = true;
        _collider.enabled = false;
        _renderer.enabled = false;
        var trigger = transform.GetChild(0);
        trigger.GetComponent<BarrelCollider>().server = true;
        trigger.gameObject.SetActive(true);
        var sphere = trigger.GetComponent<SphereCollider>();
        RpcExlodeBarrel(InitialPosAndTag, sphere.transform.TransformPoint(sphere.center), ExplosionFeedbackPool.WorldRadius(sphere));
        StartCoroutine(Decay());
    }

    private IEnumerator Decay()
    {
        yield return new WaitForSeconds(0.2f);
        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    public void RpcExlodeBarrel(string posAndTag, Vector3 center, float radius)
    {
        _collider.enabled = false;
        _renderer.enabled = false;
        ExplosionFeedbackPool.Play(_explosionPrefab, center, radius, Quaternion.Euler(-90f, 0f, 0f));

        GridPoint point;
        if (posAndTag != null && GameManager.Instance.BuildingPoints.TryGetValue(posAndTag, out point))
            point.Buildable = true;
    }
}

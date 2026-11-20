using System.Collections;
using UnityEngine;
using Mirror;

/// <summary>Owns server grenade fuse/explosion activation and replicated explosion presentation.</summary>
public class Grenade : NetworkBehaviour
{
    [SerializeField] private float _secsToExplosion;
    [SerializeField] private float _decayTime;
    public float _damage;
    [SerializeField] GameObject _explosionPrefab;
    private MeshRenderer _renderer;
    private CapsuleCollider _collider;

    void Awake()
    {
        _collider = GetComponent<CapsuleCollider>();
        _renderer = transform.GetChild(0).GetChild(0).GetComponent<MeshRenderer>();
    }

    void Start()
    {
        if (!isServer) return;
        transform.GetChild(1).GetComponent<GrenadeCollider>().server = true;
        StartCoroutine(Explode());
    }

    private IEnumerator Explode()
    {
        yield return new WaitForSeconds(_secsToExplosion);
        HideForExplosion();
        transform.GetChild(1).gameObject.SetActive(true);
        if (isServer)
        {
            var sphere = transform.GetChild(1).GetComponent<SphereCollider>();
            RpcExlode(sphere.transform.TransformPoint(sphere.center), ExplosionFeedbackPool.WorldRadius(sphere));
            StartCoroutine(Decay());
        }
    }

    // Spawned object: the server despawns it for everyone (clients used to destroy their own copies).
    private IEnumerator Decay()
    {
        yield return new WaitForSeconds(_decayTime);
        NetworkServer.Destroy(gameObject);
    }

    void HideForExplosion()
    {
        _collider.enabled = false;
        _renderer.enabled = false;
        var body = GetComponent<Rigidbody>();
        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.isKinematic = true;
        }
    }

    [ClientRpc]
    void RpcExlode(Vector3 center, float radius)
    {
        HideForExplosion();
        ExplosionFeedbackPool.Play(_explosionPrefab, center, radius, transform.rotation);
    }
}

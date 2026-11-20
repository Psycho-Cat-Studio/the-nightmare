using System.Collections;
using UnityEngine;
using Mirror;

/// <summary>Owns an authoritative single-trigger barrel explosion and releases its reserved cell before removal.</summary>
public class Barrel : NetworkBehaviour, IPlacedItem, IExplosiveHitTarget, IMatchParticipant
{
    public override void OnStartClient() { MirrorMatchBinding.Bind(gameObject); base.OnStartClient(); }

    public override void OnStartServer() { MirrorMatchBinding.Bind(gameObject); base.OnStartServer(); }

    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    public uint EntityId => netId;
    public void InitializeCell(string cellId) { InitialPosAndTag = cellId; }

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
        _game.World.ReleaseBuildingPoint(InitialPosAndTag);
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

        if (_game != null) _game.World.ReleaseBuildingPoint(posAndTag);
    }
}

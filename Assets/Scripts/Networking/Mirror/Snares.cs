using UnityEngine;
using Mirror;

/// <summary>Counts accepted brick freezes and frees the authoritative cell when the trap is consumed.</summary>
public class Snares : NetworkBehaviour, IPlacedItem, ITrapConsumable, IMatchParticipant
{
    public override void OnStartClient() { MirrorMatchBinding.Bind(gameObject); base.OnStartClient(); }

    public override void OnStartServer() { MirrorMatchBinding.Bind(gameObject); base.OnStartServer(); }

    private GameManager _game;
    public void InitializeMatch(GameManager game) { _game = game; }

    public float FreezeSeconds => freezeTime;
    public void RegisterUse() { RegisterCaughtEnemy(); }
    public void InitializeCell(string cellId) { InitialPosAndTag = cellId; }

    public float freezeTime;
    [SerializeField] private int _enemiesToDestroy = 3;
    public int EnemiesToDestroy { get { return _enemiesToDestroy; } set { _enemiesToDestroy = value; } }
    public int EnemiesCounter { get; set; }
    public string InitialPosAndTag;
    private bool _destroyed;

    private void Awake()
    {
        EnemiesCounter = 0;
    }

    // Server: counts a caught enemy and despawns the snares once used up.
    [Server]
    public void RegisterCaughtEnemy()
    {
        EnemiesCounter++;
        if (EnemiesCounter < EnemiesToDestroy || _destroyed) return;
        _destroyed = true;
        // Server state must not depend on an RPC queued on an object about to be destroyed.
        _game.World.ReleaseBuildingPoint(InitialPosAndTag);
        RpcFreeBuildingPoint(InitialPosAndTag);
        NetworkServer.Destroy(gameObject);
    }

    [ClientRpc]
    private void RpcFreeBuildingPoint(string posAndTag)
    {
        if (_game != null) _game.World.ReleaseBuildingPoint(posAndTag);
    }
}

using Mirror;
using Nightmare.Core.Match;
using UnityEngine;

/// <summary>Replicates match phases, readiness and campaign entry while GameManager owns orchestration.</summary>
[RequireComponent(typeof(GameManager))]
public sealed class MirrorMatch : NetworkBehaviour, IMatchTransport
{
    private GameManager _game;
    [SyncVar(hook = nameof(ReadyChanged))] private int _readyCount;
    public bool IsAuthority => isServer;
    public bool IsActive => NetworkServer.active;
    void Awake() { _game = GetComponent<GameManager>(); }
    public void PublishProgress(MatchProgress progress) { if (isServer) RpcProgress((int)progress.Phase, progress.StageIndex); }
    public void PublishReady(int count) { if (isServer) _readyCount = count; }
    public void BeginCampaign() { if (isServer) RpcBeginCampaign(); }
    public void Spawn(GameObject instance) { if (isServer) NetworkServer.Spawn(instance); }
    void ReadyChanged(int previous, int current) { if (_game != null) _game.ReadyPlayersCnt = current; }
    [ClientRpc] void RpcProgress(int phase, int index)
    {
        if (!isServer) _game.ApplyReplicatedProgress(new MatchProgress((MatchPhase)phase, index));
    }
    [ClientRpc] void RpcBeginCampaign() { if (_game.Players.LocalPlayer != null) _game.EnterCampaign(); }
}

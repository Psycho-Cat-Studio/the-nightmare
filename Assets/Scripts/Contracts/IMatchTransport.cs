using Nightmare.Core.Match;
using UnityEngine;

/// <summary>Authority and replication operations supplied to the match composition root.</summary>
public interface IMatchTransport
{
    /// <summary>Whether this instance may decide match state and spawn network entities.</summary>
    bool IsAuthority { get; }
    bool IsActive { get; }
    /// <summary>Replicates a phase/index already applied by the authority.</summary>
    void PublishProgress(MatchProgress progress);
    /// <summary>Replicates the authoritative unique-ready-player count.</summary>
    void PublishReady(int count);
    /// <summary>Notifies local player instances to perform campaign entry setup.</summary>
    void BeginCampaign();
    /// <summary>Publishes an authority-created object through the backend spawn mechanism.</summary>
    void Spawn(GameObject instance);
}

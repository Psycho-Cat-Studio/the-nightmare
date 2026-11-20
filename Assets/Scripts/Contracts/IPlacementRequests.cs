using UnityEngine;

public interface IPlacementRequests
{
    /// <summary>Reports local readiness; the authoritative roster deduplicates repeated requests.</summary>
    void Ready();
    /// <summary>Requests placement; inventory is spent only after an accepted placement result.</summary>
    /// <param name="position">Requested world-space position.</param>
    /// <param name="rotation">Requested world-space rotation.</param>
    /// <param name="cellId">Persistent authored cell ID, independent of culture or formatted coordinates.</param>
    /// <param name="itemIndex">Zero-based placeable catalog index.</param>
    void Place(Vector3 position, Quaternion rotation, string cellId, int itemIndex);
}

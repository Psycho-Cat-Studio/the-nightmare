using UnityEngine;

/// <summary>Injects the active scene context at Mirror spawn callbacks; gameplay code does not discover a global match.</summary>
public static class MirrorMatchBinding
{
    public static void Bind(GameObject entity)
    {
        var backend = CustomNetworkManager.Instance;
        if (backend != null && backend.Game != null) MatchBindings.BindHierarchy(entity, backend.Game);
    }
}

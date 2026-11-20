using UnityEngine;

/// <summary>Receives the session composition root before gameplay callbacks use its services.</summary>
public interface IMatchParticipant { void InitializeMatch(GameManager game); }

/// <summary>Lets a backend composition boundary attach and detach its loaded match without a global game singleton.</summary>
public interface IMatchSceneRegistration { void SetMatch(GameManager game); }

/// <summary>Injects a known match into scene or spawned-object participants without storing global state.</summary>
public static class MatchBindings
{
    /// <summary>Supplies a known session to hierarchy participants before their gameplay startup callbacks.</summary>
    public static void BindHierarchy(GameObject root, GameManager game)
    {
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (component is IMatchParticipant participant) participant.InitializeMatch(game);
    }
}

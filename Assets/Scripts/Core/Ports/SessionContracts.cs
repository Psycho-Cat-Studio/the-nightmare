using System;

namespace Nightmare.Core.Ports
{
    /// <summary>Session lifecycle and status exposed to menus without a transport dependency.</summary>
    public interface ISessionService
    {
        /// <summary>Attempts to start a local host with the requested room name and port.</summary>
        /// <returns>Whether hosting was started; this is not a guarantee that remote peers can reach it.</returns>
        bool Host(string roomName, int port);
        /// <summary>Starts connecting to a host; completion or failure is reported through StatusChanged.</summary>
        void Join(string address, int port);
        /// <summary>Leaves the active session using the backend lifecycle.</summary>
        void Leave();
        int ConnectedPlayers { get; }
        bool IsNetworkActive { get; }
        string Status { get; }
        ISessionDirectory Directory { get; }
        /// <summary>Reports user-facing connection status; subscribers must unsubscribe with their view lifetime.</summary>
        event Action<string> StatusChanged;
        /// <summary>Ends the session and returns to the application main menu.</summary>
        void LeaveToMainMenu();
    }

    /// <summary>Distinguishes simulation authority from ownership of local controls.</summary>
    public interface IAuthorityContext
    {
        bool IsSimulationAuthority { get; }
        bool IsLocalPlayer { get; }
    }

    public interface IClock { float DeltaTime { get; } }

    /// <summary>Read-only health view for presentation and local control gating.</summary>
    public interface IHealth
    {
        float CurrentHealth { get; }
        float MaximumHealth { get; }
        bool IsDead { get; }
    }
}

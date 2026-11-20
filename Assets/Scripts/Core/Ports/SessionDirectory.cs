using System;
using System.Collections.Generic;
using System.Globalization;

namespace Nightmare.Core.Ports
{
    /// <summary>Describes an advertised session and the compatibility, capacity and phase conditions required to join.</summary>
    public abstract class SessionListing
    {
        public string Name;
        public string Address;
        public int Port;
        public int Players;
        public int MaxPlayers;
        public bool InProgress;
        public string Version;
        public bool IsFull => Players >= MaxPlayers;
        public abstract bool IsCompatible { get; }
        /// <summary>Whether this advertised listing is compatible, not full, and not already in progress.</summary>
        public bool CanJoin => IsCompatible && !IsFull && !InProgress;
    }
    public interface ISessionDirectory
    {
        IEnumerable<SessionListing> Games { get; }
        bool IsListening { get; }
        event Action GamesChanged;
        void StartListening();
        void StopListening();
    }
    /// <summary>Defines the default direct-connect game port and advertised room name.</summary>
    public static class SessionDefaults
    {
        public const int Port = 7777;
        public const string RoomName = "The Nightmare";
    }
    /// <summary>Parses the supported host or host:port syntax using culture-independent port numbers.</summary>
    public static class SessionAddress
    {
        /// <summary>Parses the existing host or host:port syntax, using the default game port when omitted.</summary>
        /// <remarks>This checks address syntax and port bounds, not host reachability.</remarks>
        public static bool TryParse(string input, out string host, out int port)
        {
            host = null; port = SessionDefaults.Port;
            if (string.IsNullOrEmpty(input)) return false;
            input = input.Trim();
            int colon = input.LastIndexOf(':');
            if (colon > 0 && input.IndexOf(':') == colon)
            {
                if (!int.TryParse(input.Substring(colon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out port)
                    || port <= 0 || port > 65535) return false;
                input = input.Substring(0, colon);
            }
            host = input;
            return host.Length > 0;
        }
    }
}

using Nightmare.Core.Ports;

/// <summary>Application composition entry for menu navigation, not a gameplay service locator.</summary>
public static class SessionServices
{
    public static ISessionService Current { get; set; }
}

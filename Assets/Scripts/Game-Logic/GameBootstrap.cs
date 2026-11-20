using System.Globalization;
using System.Threading;
using UnityEngine;

/// <summary>Initializes invariant culture for legacy numeric identifiers and consistent game formatting.</summary>
public static class GameBootstrap
{
    // Capture areas and building points are identified by strings built from float positions
    // ("1.5_0_2" vs "1,5_0_2"), and those ids are sent over the network. Players with different
    // system locales (e.g. Polish Windows and English macOS) produced different ids and the
    // receiving side threw KeyNotFoundException. Using one culture everywhere avoids that.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void UseInvariantCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
        Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
    }
}

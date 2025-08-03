using System;
using UnlimitedMages.System.Components;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.API;

/// <summary>
///     Public API for other mods to interact with Unlimited Mages.
///     Provides access to key mod information and events in a safe, read-only manner.
/// </summary>
public static class UnlimitedMagesAPI
{
    /// <summary>
    ///     Checks if the Unlimited Mages mod is loaded and its core systems are ready.
    ///     Other mods should check this property before accessing any other part of the API.
    /// </summary>
    /// <returns>True if the API is ready for use; otherwise, false.</returns>
    public static bool IsReady => ConfigManager.Instance != null && ConfigManager.Instance.IsConfigReady;

    /// <summary>
    ///     Gets the current team size configured by the host.
    ///     Returns the default game team size if the mod is not ready.
    /// </summary>
    public static int TeamSize => ConfigManager.Instance?.TeamSize ?? GameConstants.Game.OriginalTeamSize;

    /// <summary>
    ///     Fired when the host's configuration is finalized and received by the client.
    ///     Provides the configured team size.
    /// </summary>
    public static event Action<int>? OnConfigReady;

    internal static void RaiseConfigReady(int teamSize)
    {
        OnConfigReady?.Invoke(teamSize);
    }
}
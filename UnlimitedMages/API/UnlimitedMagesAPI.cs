using System;
using UnlimitedMages.System.Components;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.API;

/// <summary>
///     Provides a public-facing API for other mods to interact with the Unlimited Mages mod.
///     Allows checking mod state and listening for configuration changes.
/// </summary>
public static class UnlimitedMagesAPI
{
    /// <summary>
    ///     Gets a value indicating whether the mod's configuration has been synchronized between the host and clients.
    /// </summary>
    /// <value><c>true</c> if the mod is ready; otherwise, <c>false</c>.</value>
    public static bool IsReady => ConfigManager.Instance != null && ConfigManager.Instance.IsConfigReady;

    /// <summary>
    ///     Gets the currently configured team size for the lobby.
    ///     Returns the original game's team size if the mod is not ready.
    /// </summary>
    public static int TeamSize => ConfigManager.Instance?.TeamSize ?? GameConstants.Game.OriginalTeamSize;

    /// <summary>
    ///     An event that is invoked when the mod's configuration is finalized and ready.
    ///     Provides the configured team size as an argument.
    /// </summary>
    public static event Action<int>? OnConfigReady;

    /// <summary>
    ///     Internally raises the <see cref="OnConfigReady" /> event to notify external listeners.
    /// </summary>
    /// <param name="teamSize">The finalized team size.</param>
    internal static void RaiseConfigReady(int teamSize)
    {
        OnConfigReady?.Invoke(teamSize);
    }
}
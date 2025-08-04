using BepInEx.Logging;
using UnityEngine;
using UnlimitedMages.API;
using UnlimitedMages.Components;
using UnlimitedMages.System.Commands.Types;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.System.Components;

/// <summary>
///     A singleton component that manages the session's runtime configuration, such as the team size.
///     It serves as the single source of truth for configuration values once they are synchronized between host and clients.
/// </summary>
internal sealed class ConfigManager : MonoBehaviour, IModComponent
{
    /// <summary>
    ///     Gets the singleton instance of the ConfigManager.
    /// </summary>
    public static ConfigManager? Instance { get; private set; }

    /// <summary>
    ///     Gets the synchronized team size for the current lobby.
    /// </summary>
    public int TeamSize { get; private set; } = GameConstants.Game.MinimumTeamSize;

    /// <summary>
    ///     Gets a value indicating whether the configuration has been finalized for the current session.
    /// </summary>
    public bool IsConfigReady { get; private set; }

    /// <summary>
    ///     Resets the configuration to its default state. Called when entering a new lobby or returning to the menu.
    /// </summary>
    public void Reset()
    {
        UnlimitedMagesPlugin.Log?.LogInfo("Resetting session configuration.");
        IsConfigReady = false;
        TeamSize = GameConstants.Game.MinimumTeamSize;
    }

    /// <summary>
    ///     Initializes the singleton instance of the ConfigManager.
    /// </summary>
    public void Initialize(ManualLogSource log)
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    /// <summary>
    ///     Finalizes the configuration with a specific team size. Once finalized, the configuration is considered ready.
    ///     This method publishes events to notify other systems of the finalized configuration.
    /// </summary>
    /// <param name="teamSize">The team size to set.</param>
    public void FinalizeConfig(int teamSize)
    {
        if (IsConfigReady && teamSize == TeamSize) return;

        TeamSize = teamSize;
        IsConfigReady = true;
        UnlimitedMagesPlugin.Log?.LogInfo($"Config finalized. Team Size: {TeamSize}");

        // Notify internal systems.
        EventBus.Publish(new ConfigReadyEvent(TeamSize));
        // Notify external mods via the public API.
        UnlimitedMagesAPI.RaiseConfigReady(teamSize);

        var sessionManager = SessionManager.Instance;
        if (sessionManager == null || sessionManager.IsHost()) return;

        // If this is a client, finalizing the config means it's time to request the full lobby state.
        UnlimitedMagesPlugin.Log?.LogInfo("[Client] Config is ready. Requesting full lobby state from host.");
        sessionManager.SendCommand(new RequestLobbyStateCommand());
    }
}
using System;
using System.Collections;
using BepInEx.Logging;
using Dissonance;
using Dissonance.Networking;
using Steamworks;
using UnityEngine;
using UnlimitedMages.Components;
using UnlimitedMages.UI;

namespace UnlimitedMages.Networking;

/// <summary>
///     Manages the synchronization of mod configuration (e.g., team size) across the network.
///     It ensures that all clients in a lobby use the same settings as the host.
/// </summary>
public class NetworkedConfigManager : MonoBehaviour, IModComponent
{
    private DissonanceComms? _comms;
    private bool _networkStartedAndHandled;

    /// <summary>
    ///     Singleton instance of the manager, providing easy global access.
    /// </summary>
    public static NetworkedConfigManager? Instance { get; private set; }

    /// <summary>
    ///     The synchronized team size for the current match. Authoritative on the host.
    /// </summary>
    public int TeamSize { get; private set; }

    private void Update()
    {
        if (_networkStartedAndHandled || _comms == null || !_comms.IsNetworkInitialized) return;
        _networkStartedAndHandled = true;

        if (IsHost())
            // We are the host, so broadcast the initial team size that was selected in the UI.
            SetAndBroadcastTeamSize(UISliderInjector.SelectedTeamSize);
        else
            // We are a client, so request the config from the host.
            RequestConfigFromHost();
    }

    private void OnDestroy()
    {
        if (_comms != null)
            _comms.Text.MessageReceived -= OnChatMessageReceived;
    }

    /// <summary>
    ///     Initializes the singleton instance and starts the coroutine to find the DissonanceComms object.
    /// </summary>
    public void Initialize(ManualLogSource log)
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;

        StartCoroutine(FindDissonance());
    }

    /// <summary>
    ///     Event invoked when the team size is updated from the network.
    ///     UI and game logic systems subscribe to this to react to changes.
    /// </summary>
    public static event Action<int>? OnTeamSizeChanged;

    private IEnumerator FindDissonance()
    {
        UnlimitedMagesPlugin.Log?.LogInfo("Searching for DissonanceComms...");
        while (_comms == null)
        {
            _comms = FindFirstObjectByType<DissonanceComms>();
            if (_comms == null)
                yield return new WaitForSeconds(1f);
        }

        UnlimitedMagesPlugin.Log?.LogInfo("DissonanceComms found! Subscribing to text messages.");
        _comms.Text.MessageReceived += OnChatMessageReceived;
    }

    private void OnChatMessageReceived(TextMessage message)
    {
        // Filter messages to only those relevant to this mod.
        if (!message.Message.StartsWith("[UNLIMITED_MAGES]")) return;

        string[] parts = message.Message.Split(':');
        if (parts.Length < 1) return;

        var command = parts[0];

        if (command == "[UNLIMITED_MAGES]REQUEST_CONFIG" && IsHost())
        {
            UnlimitedMagesPlugin.Log?.LogInfo($"[Host] Received config request from a client. Sending team size: {TeamSize}");
            SetAndBroadcastTeamSize(TeamSize);
        }

        if (command != "[UNLIMITED_MAGES]SET_TEAM_SIZE") return;
        if (parts.Length <= 1 || !int.TryParse(parts[1], out var newSize)) return;

        UnlimitedMagesPlugin.Log?.LogInfo($"[Client] Received team size from host: {newSize}");
        TeamSize = newSize;
        OnTeamSizeChanged?.Invoke(newSize);
    }

    private void RequestConfigFromHost()
    {
        if (_comms == null || !_comms.IsNetworkInitialized) return;

        UnlimitedMagesPlugin.Log?.LogInfo("[Client] Requesting config from host...");
        _comms.Text.Send("Global", "[UNLIMITED_MAGES]REQUEST_CONFIG");
    }

    /// <summary>
    ///     Sets the local team size and, if the current player is the host, broadcasts
    ///     the new value to all clients in the Dissonance channel.
    /// </summary>
    /// <param name="newSize">The new size for each team.</param>
    public void SetAndBroadcastTeamSize(int newSize)
    {
        if (TeamSize != newSize)
        {
            TeamSize = newSize;
            OnTeamSizeChanged?.Invoke(newSize);
        }

        if (_comms == null || !_comms.IsNetworkInitialized || !IsHost()) return;

        var msg = $"[UNLIMITED_MAGES]SET_TEAM_SIZE:{newSize}";
        _comms.Text.Send("Global", msg);
        UnlimitedMagesPlugin.Log?.LogInfo($"[Host] Broadcasting team size: {newSize}");
    }

    private bool IsHost()
    {
        return BootstrapManager.CurrentLobbyID != 0 && SteamMatchmaking.GetLobbyOwner(new CSteamID(BootstrapManager.CurrentLobbyID)) == SteamUser.GetSteamID();
    }
}
using System.Collections;
using BepInEx.Logging;
using Dissonance;
using Dissonance.Networking;
using Steamworks;
using UnityEngine;
using UnlimitedMages.Components;
using UnlimitedMages.System.Components;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using UnlimitedMages.UI;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.Networking;

internal sealed class SessionManager : MonoBehaviour, IModComponent
{
    private DissonanceComms? _comms;
    private Coroutine? _findDissonanceCoroutine;
    private bool _networkStartedAndHandled;

    public static SessionManager? Instance { get; private set; }

    private void Update()
    {
        if (_networkStartedAndHandled || _comms == null || !_comms.IsNetworkInitialized || ConfigManager.Instance == null) return;
        _networkStartedAndHandled = true;

        if (IsHost())
        {
            UnlimitedMagesPlugin.Log?.LogInfo("[Host] Network initialized. Setting local config and preparing to broadcast.");

            SetAndBroadcastTeamSize(UnlimitedMagesSlider.SelectedTeamSize);
        }
        else // If we are a client, we request the config from the host.
        {
            UnlimitedMagesPlugin.Log?.LogInfo("[Client] Network initialized. Requesting config from host.");
            RequestConfigFromHost();
        }
    }

    private void OnDestroy()
    {
        // Ensure that the coroutine is not somehow running in the background.
        if (_findDissonanceCoroutine != null)
        {
            StopCoroutine(_findDissonanceCoroutine);
            _findDissonanceCoroutine = null;
        }

        if (_comms != null)
            _comms.Text.MessageReceived -= OnChatMessageReceived;

        EventBus.Unsubscribe<HostTeamSizeChangedEvent>(OnHostTeamSizeChanged);
    }

    public void Initialize(ManualLogSource log)
    {
        if (Instance != null)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        EventBus.Subscribe<HostTeamSizeChangedEvent>(OnHostTeamSizeChanged);
        _findDissonanceCoroutine = StartCoroutine(FindDissonance());
    }

    private void OnHostTeamSizeChanged(HostTeamSizeChangedEvent evt)
    {
        SetAndBroadcastTeamSize(evt.NewTeamSize);
    }

    private IEnumerator FindDissonance()
    {
        UnlimitedMagesPlugin.Log?.LogInfo("Searching for DissonanceComms...");
        while (_comms == null)
        {
            _comms = FindFirstObjectByType<DissonanceComms>();
            if (_comms == null)
                yield return new WaitForSeconds(GameConstants.Networking.DissonanceSearchInterval);
        }

        UnlimitedMagesPlugin.Log?.LogInfo("DissonanceComms found! Subscribing to text messages.");
        _comms.Text.MessageReceived += OnChatMessageReceived;
    }

    private void OnChatMessageReceived(TextMessage message)
    {
        if (!message.Message.StartsWith(GameConstants.Networking.CommandPrefix) || ConfigManager.Instance == null) return;

        var parts = message.Message.Split(GameConstants.Networking.CommandDelimiter);
        if (parts.Length < 1) return;

        var command = parts[0];

        switch (command)
        {
            case $"{GameConstants.Networking.CommandPrefix}REQUEST_CONFIG" when IsHost():
                var currentTeamSize = ConfigManager.Instance.TeamSize;
                UnlimitedMagesPlugin.Log?.LogInfo($"[Host] Received config request. Sending team size: {currentTeamSize}");
                BroadcastTeamSize(currentTeamSize);
                break;

            case $"{GameConstants.Networking.CommandPrefix}SET_TEAM_SIZE":
            {
                if (ConfigManager.Instance.IsConfigReady) return; // Already configured, ignore.
                if (parts.Length <= 1 || !int.TryParse(parts[1], out var newSize)) return;

                UnlimitedMagesPlugin.Log?.LogInfo($"[Client] Received team size from host: {newSize}. Finalizing config.");
                ConfigManager.Instance.FinalizeConfig(newSize);
                break;
            }
        }
    }

    private void RequestConfigFromHost()
    {
        if (_comms == null || !_comms.IsNetworkInitialized) return;
        _comms.Text.Send("Global", $"{GameConstants.Networking.CommandPrefix}REQUEST_CONFIG");
    }

    public void SetAndBroadcastTeamSize(int newSize)
    {
        if (!IsHost() || ConfigManager.Instance == null) return;

        ConfigManager.Instance.FinalizeConfig(newSize);

        BroadcastTeamSize(newSize);
    }

    private void BroadcastTeamSize(int size)
    {
        if (_comms == null || !_comms.IsNetworkInitialized || !IsHost()) return;

        var msg = $"{GameConstants.Networking.CommandPrefix}SET_TEAM_SIZE:{size}";
        _comms.Text.Send("Global", msg);
        UnlimitedMagesPlugin.Log?.LogInfo($"[Host] Broadcasting team size: {size}");
    }

    private bool IsHost()
    {
        return BootstrapManager.CurrentLobbyID != 0 && SteamMatchmaking.GetLobbyOwner(new CSteamID(BootstrapManager.CurrentLobbyID)) == SteamUser.GetSteamID();
    }
}
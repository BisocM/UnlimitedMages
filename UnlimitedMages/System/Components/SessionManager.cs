using System.Collections;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Dissonance;
using Dissonance.Networking;
using Steamworks;
using UnityEngine;
using UnlimitedMages.Components;
using UnlimitedMages.System.Commands;
using UnlimitedMages.System.Commands.Attributes;
using UnlimitedMages.System.Commands.Types;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using UnlimitedMages.UI.Lobby;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.System.Components;

/// <summary>
///     A singleton component that manages the network session for the mod.
///     It handles finding the Dissonance voice comms system, dispatching commands received over chat,
///     and orchestrating the initial configuration sync between host and clients.
/// </summary>
internal sealed class SessionManager : MonoBehaviour, IModComponent
{
    private CommandDispatcher _commandDispatcher = null!;
    private Coroutine? _findDissonanceCoroutine;
    private bool _networkStartedAndHandled;

    /// <summary>
    ///     Gets the singleton instance of the SessionManager.
    /// </summary>
    public static SessionManager? Instance { get; private set; }

    /// <summary>
    ///     Gets the cached DissonanceComms component instance.
    /// </summary>
    public DissonanceComms? Comms { get; private set; }

    /// <summary>
    ///     Gets or sets a value indicating whether the initial lobby state has been synchronized for this client.
    /// </summary>
    public bool LobbyStateSynced { get; internal set; }

    /// <summary>
    ///     Resets the session manager's state, preparing it for a new lobby session.
    /// </summary>
    public void Reset()
    {
        UnlimitedMagesPlugin.Log?.LogInfo("Resetting SessionManager state for new session.");
        _networkStartedAndHandled = false;
        LobbyStateSynced = false;

        // Unsubscribe from the old Dissonance instance if it exists.
        if (Comms != null)
        {
            Comms.Text.MessageReceived -= OnChatMessageReceived;
            UnlimitedMagesPlugin.Log?.LogDebug("Unsubscribed from previous DissonanceComms instance.");
        }

        // Stop any existing search coroutine and start a new one.
        if (_findDissonanceCoroutine != null) StopCoroutine(_findDissonanceCoroutine);

        Comms = null;
        _findDissonanceCoroutine = StartCoroutine(FindDissonance());
    }

    /// <summary>
    ///     In the Update loop, checks for when the network is initialized to begin the mod's handshake process.
    ///     The host broadcasts its configuration, and clients request it.
    /// </summary>
    private void Update()
    {
        if (_networkStartedAndHandled || Comms == null || !Comms.IsNetworkInitialized || ConfigManager.Instance == null) return;
        _networkStartedAndHandled = true;

        if (IsHost())
        {
            UnlimitedMagesPlugin.Log?.LogInfo("[Host] Network initialized. Setting local config and preparing to broadcast.");
            SetAndBroadcastTeamSize(UnlimitedMagesSlider.SelectedTeamSize);
            LobbyStateSynced = true; // Host is always synced with itself.
        }
        else
        {
            UnlimitedMagesPlugin.Log?.LogInfo("[Client] Network initialized. Requesting config from host.");
            SendCommand(new RequestConfigCommand());
        }
    }

    private void OnDestroy()
    {
        if (_findDissonanceCoroutine != null)
        {
            StopCoroutine(_findDissonanceCoroutine);
            _findDissonanceCoroutine = null;
        }

        if (Comms != null)
            Comms.Text.MessageReceived -= OnChatMessageReceived;

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
        _commandDispatcher = new CommandDispatcher();
        EventBus.Subscribe<HostTeamSizeChangedEvent>(OnHostTeamSizeChanged);
        _findDissonanceCoroutine = StartCoroutine(FindDissonance());
    }

    /// <summary>
    ///     Handles the event fired when the host changes the team size via the UI slider.
    /// </summary>
    private void OnHostTeamSizeChanged(HostTeamSizeChangedEvent evt)
    {
        SetAndBroadcastTeamSize(evt.NewTeamSize);
    }

    /// <summary>
    ///     A coroutine that periodically searches for the DissonanceComms instance until it is found.
    ///     Once found, it subscribes to the text message received event.
    /// </summary>
    private IEnumerator FindDissonance()
    {
        UnlimitedMagesPlugin.Log?.LogInfo("Searching for DissonanceComms...");
        while (Comms == null)
        {
            Comms = FindFirstObjectByType<DissonanceComms>();
            if (Comms == null)
                yield return new WaitForSeconds(GameConstants.Networking.DissonanceSearchInterval);
        }

        UnlimitedMagesPlugin.Log?.LogInfo("DissonanceComms found! Subscribing to text messages.");
        Comms.Text.MessageReceived += OnChatMessageReceived;
    }

    /// <summary>
    ///     Callback for when a Dissonance text message is received. If it's a mod command, it's dispatched.
    /// </summary>
    private void OnChatMessageReceived(TextMessage message)
    {
        if (!message.Message.StartsWith(GameConstants.Networking.CommandPrefix) || ConfigManager.Instance == null) return;
        _commandDispatcher.Dispatch(message.Message, message.Sender);
    }

    /// <summary>
    ///     Finalizes the host's configuration and broadcasts the new team size to all clients.
    /// </summary>
    public void SetAndBroadcastTeamSize(int newSize)
    {
        if (!IsHost() || ConfigManager.Instance == null) return;

        ConfigManager.Instance.FinalizeConfig(newSize);

        SendCommand(new SetTeamSizeCommand { Payload = newSize.ToString() });
        UnlimitedMagesPlugin.Log?.LogInfo($"[Host] Broadcasting team size: {newSize}");
    }

    /// <summary>
    ///     Sends a command object over the network using Dissonance's text chat channel.
    /// </summary>
    /// <typeparam name="T">The type of the command to send.</typeparam>
    /// <param name="command">The command instance to send.</param>
    public void SendCommand<T>(T command) where T : ICommand
    {
        if (Comms == null) return;

        var commandType = typeof(T);
        var attribute = commandType.GetCustomAttribute<ChatCommandAttribute>();
        if (attribute == null)
        {
            UnlimitedMagesPlugin.Log?.LogError($"Cannot send command '{commandType.Name}' because it lacks a [ChatCommand] attribute.");
            return;
        }

        var commandName = attribute.CommandName;

        // Serialize the command into a string format: "[PREFIX]COMMAND_NAME:Payload"
        var payloadProperty = commandType.GetProperties().FirstOrDefault();
        var payload = payloadProperty?.GetValue(command)?.ToString();

        var message = $"{GameConstants.Networking.CommandPrefix}{commandName}";
        if (!string.IsNullOrEmpty(payload)) message += $"{GameConstants.Networking.CommandDelimiter}{payload}";

        Comms.Text.Send("Global", message);
    }

    /// <summary>
    ///     Checks if the local player is the host of the current Steam lobby.
    /// </summary>
    public bool IsHost() => BootstrapManager.CurrentLobbyID != 0 && SteamMatchmaking.GetLobbyOwner(new CSteamID(BootstrapManager.CurrentLobbyID)) == SteamUser.GetSteamID();
}
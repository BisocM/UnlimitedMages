using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Steamworks;
using UnityEngine;
using UnlimitedMages.Components;
using UnlimitedMages.System.Commands.Types;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using UnlimitedMages.UI.Lobby;
using UnlimitedMages.Utilities;
using Random = System.Random;

namespace UnlimitedMages.System.Components;

/// <summary>
///     A singleton component that manages the state of the lobby, including all players,
///     their team assignments, and their ready status. It serves as the client-side source of truth for lobby data.
/// </summary>
internal class LobbyStateManager : MonoBehaviour, IModComponent
{
    // A temporary list for players who join before the config is ready.
    private readonly List<PlayerLobbyData> _pendingPlayers = new();

    /// <summary>
    ///     A list of all players currently in the lobby.
    /// </summary>
    public readonly List<PlayerLobbyData> AllPlayers = new();

    /// <summary>
    ///     A dictionary mapping team IDs (0 or 2) to arrays representing the player slots for that team.
    /// </summary>
    public readonly Dictionary<int, PlayerLobbyData?[]> Teams = new();

    private bool _isStateReady;

    private ManualLogSource? _log;
    private MainMenuManager? _mainMenuManager;

    /// <summary>
    ///     Gets the singleton instance of the LobbyStateManager.
    /// </summary>
    public static LobbyStateManager? Instance { get; private set; }

    /// <summary>
    ///     Resets the state of the manager, clearing all player and team data.
    /// </summary>
    public void Reset()
    {
        _log?.LogInfo("Resetting LobbyStateManager state for new session.");
        AllPlayers.Clear();
        Teams.Clear();
        _pendingPlayers.Clear();
        _isStateReady = false;
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<ConfigReadyEvent>(OnConfigReady);
        if (Instance == this) Instance = null;
    }

    public void Initialize(ManualLogSource log)
    {
        if (Instance != null)
        {
            log.LogWarning("Duplicate LobbyStateManager instance detected. Destroying this component.");
            Destroy(this);
            return;
        }

        Instance = this;
        _log = log;

        EventBus.Subscribe<ConfigReadyEvent>(OnConfigReady);
        _log.LogInfo("LobbyStateManager initialized and subscribed to config events.");
    }

    /// <summary>
    ///     Sets the reference to the active MainMenuManager instance.
    /// </summary>
    public void SetMainMenuManager(MainMenuManager manager)
    {
        _mainMenuManager = manager;
        _log?.LogDebug("MainMenuManager instance has been set.");
    }

    /// <summary>
    ///     Handles the ConfigReadyEvent to initialize team structures and process any pending players.
    /// </summary>
    private void OnConfigReady(ConfigReadyEvent evt)
    {
        if (_isStateReady) return;

        _log?.LogInfo($"Configuration ready. Initializing teams with size {evt.TeamSize}.");
        InitializeTeams(evt.TeamSize);
        _isStateReady = true;

        // Process players who joined before the team size was known.
        if (_pendingPlayers.Count > 0)
        {
            _log?.LogInfo($"Processing {_pendingPlayers.Count} pending players...");
            foreach (var player in _pendingPlayers) AddPlayer(player);

            _pendingPlayers.Clear();
        }
    }

    /// <summary>
    ///     Initializes the team data structures with the specified size.
    /// </summary>
    internal void InitializeTeams(int teamSize)
    {
        Teams[0] = new PlayerLobbyData?[teamSize];
        Teams[2] = new PlayerLobbyData?[teamSize];
        _log?.LogInfo($"LobbyStateManager team structures initialized for size {teamSize}.");
    }

    /// <summary>
    ///     Adds a player to the lobby state using their details. If the state is not ready, the player is queued.
    /// </summary>
    public void AddPlayer(string fullName, string rank, string steamId)
    {
        // For the local player, the name might be empty initially. Fetch it from Steam.
        if (string.IsNullOrEmpty(fullName) && steamId == SteamUser.GetSteamID().ToString())
        {
            _log?.LogInfo("Local player detected with empty name. Fetching from Steam...");
            fullName = SteamFriends.GetPersonaName();
        }

        var clampedName = ClampString(fullName, GameConstants.MainMenuManager.PlayerNameClampLength);
        var player = new PlayerLobbyData(fullName, clampedName, rank, steamId);

        if (!_isStateReady)
        {
            _log?.LogDebug($"State not ready. Queuing player '{fullName}' for later processing.");
            _pendingPlayers.Add(player);
            return;
        }

        AddPlayer(player);
    }

    /// <summary>
    ///     The core logic for adding or updating a player in the master list.
    /// </summary>
    private void AddPlayer(PlayerLobbyData player)
    {
        // Check if player already exists (by Steam ID or, as a fallback, by name) and update their info.
        if (!string.IsNullOrEmpty(player.SteamId) && player.SteamId != "0")
        {
            if (AllPlayers.Any(p => p.SteamId == player.SteamId))
            {
                _log?.LogDebug($"Player '{player.FullName}' ({player.SteamId}) already exists. Updating info.");
                var existingPlayer = AllPlayers.First(p => p.SteamId == player.SteamId);
                existingPlayer.Rank = player.Rank;
                return;
            }
        }
        else
        {
            if (AllPlayers.Any(p => p.ClampedName == player.ClampedName))
            {
                _log?.LogDebug($"Player '{player.ClampedName}' (no SteamID) already exists. Updating info.");
                var existingPlayer = AllPlayers.First(p => p.ClampedName == player.ClampedName);
                existingPlayer.Rank = player.Rank;
                return;
            }
        }

        AllPlayers.Add(player);
        _log?.LogInfo($"Player '{player.FullName}' added to the lobby state.");
    }

    /// <summary>
    ///     Removes a player from the lobby state based on their clamped (in-game) name.
    /// </summary>
    public void RemovePlayer(string clampedName)
    {
        var playerToRemove = AllPlayers.FirstOrDefault(p => p.ClampedName == clampedName);
        if (playerToRemove == null)
        {
            _log?.LogWarning($"Attempted to remove player with clamped name '{clampedName}', but they were not found.");
            return;
        }

        AllPlayers.Remove(playerToRemove);
        ClearPlayerFromAllTeams(playerToRemove.SteamId!);
        _log?.LogInfo($"Player '{playerToRemove.FullName}' removed from the lobby state.");
    }

    /// <summary>
    ///     Assigns a player to a specific team and slot. This will also remove them from any previous slot.
    /// </summary>
    public void AssignPlayerToTeam(int teamId, int slot, string fullName, string rankAndLevel)
    {
        if (!Teams.TryGetValue(teamId, out var teamArray) || slot < 0 || slot >= teamArray.Length)
        {
            _log?.LogWarning($"Attempted to assign player to an invalid team/slot. Team: {teamId}, Slot: {slot}");
            return;
        }

        var clampedName = ClampString(fullName, GameConstants.MainMenuManager.PlayerNameClampLength);
        var player = AllPlayers.FirstOrDefault(p => p.ClampedName == clampedName);

        if (player == null)
        {
            _log?.LogError($"Cannot assign player '{fullName}' to team {teamId}. Player not found in master list!");
            return;
        }

        player.Rank = rankAndLevel;

        // Ensure the player isn't in two places at once.
        ClearPlayerFromAllTeams(player.SteamId!);

        teamArray[slot] = player;
        _log?.LogInfo($"Assigned player '{player.FullName}' to team {teamId}, slot {slot}.");
    }

    /// <summary>
    ///     Clears a player from a specific team slot, making it empty.
    /// </summary>
    public void ClearPlayerFromTeamSlot(int teamId, int slot)
    {
        if (!Teams.TryGetValue(teamId, out var teamArray) || slot < 0 || slot >= teamArray.Length)
        {
            _log?.LogWarning($"Attempted to clear an invalid team/slot. Team: {teamId}, Slot: {slot}");
            return;
        }

        var player = teamArray[slot];
        if (player != null)
        {
            teamArray[slot] = null;
            _log?.LogInfo($"Cleared player '{player.FullName}' from team {teamId}, slot {slot}.");
        }
    }

    /// <summary>
    ///     Gets a list of all players who are not currently assigned to any team.
    /// </summary>
    public IEnumerable<PlayerLobbyData> GetUnassignedPlayers()
    {
        var assignedSteamIds = Teams.Values.SelectMany(team => team)
            .Where(p => p != null)
            .Select(p => p!.SteamId)
            .ToHashSet();

        return AllPlayers.Where(p => !assignedSteamIds.Contains(p.SteamId));
    }

    /// <summary>
    ///     Helper method to remove a player from any team they might be on.
    /// </summary>
    private void ClearPlayerFromAllTeams(string steamId)
    {
        foreach (var teamId in Teams.Keys)
            for (var i = 0; i < Teams[teamId].Length; i++)
                if (Teams[teamId][i]?.SteamId == steamId)
                    Teams[teamId][i] = null;
    }

    /// <summary>
    ///     Checks if the local player is the host of the current Steam lobby.
    /// </summary>
    public bool IsHost() => BootstrapManager.CurrentLobbyID != 0 && SteamMatchmaking.GetLobbyOwner(new CSteamID(BootstrapManager.CurrentLobbyID)) == SteamUser.GetSteamID();

    /// <summary>
    ///     Opens the Steam overlay to the profile of the specified user.
    /// </summary>
    public void OpenSteamProfile(string steamId)
    {
        if (ulong.TryParse(steamId, out var id))
        {
            _log?.LogInfo($"Opening Steam profile for user ID {steamId}.");
            SteamFriends.ActivateGameOverlayToUser("steamid", new CSteamID(id));
        }
    }

    /// <summary>
    ///     Kicks a player from the lobby. This can only be called by the host.
    /// </summary>
    public void KickPlayer(string steamId)
    {
        if (_mainMenuManager == null)
        {
            _log?.LogError("Cannot kick player: MainMenuManager is not available.");
            return;
        }

        _log?.LogInfo($"Host is attempting to kick player with Steam ID {steamId}.");
        _mainMenuManager.KickPlayer(steamId);
    }

    /// <summary>
    ///     Automatically distributes all players in the lobby between the two teams as evenly as possible.
    ///     This can only be called by the host.
    /// </summary>
    public void BalanceTeams()
    {
        if (!IsHost() || _mainMenuManager?.mmmn == null)
        {
            _log?.LogWarning("BalanceTeams called by non-host, or MainMenuManager components are not available.");
            return;
        }

        if (ConfigManager.Instance == null)
        {
            _log?.LogError("ConfigManager is not available. Cannot determine team size for balancing.");
            return;
        }

        _log?.LogInfo("Host has initiated team balancing.");

        // Get a shuffled list of all players to randomize team assignment.
        var playersToAssign = new List<PlayerLobbyData>(AllPlayers);
        var rng = new Random();
        var shuffledPlayers = playersToAssign.OrderBy(_ => rng.Next()).ToList();

        var teamSize = ConfigManager.Instance.TeamSize;
        var team1Count = 0;
        var team2Count = 0;

        var unassignedPlayers = new List<PlayerLobbyData>(shuffledPlayers);

        // Assign players one by one to the team with fewer members.
        foreach (var player in shuffledPlayers)
        {
            var preferredTeamId = team1Count <= team2Count ? 0 : 2;
            var fallbackTeamId = preferredTeamId == 0 ? 2 : 0;

            var preferredTeamCount = preferredTeamId == 0 ? team1Count : team2Count;
            var fallbackTeamCount = fallbackTeamId == 0 ? team1Count : team2Count;

            if (preferredTeamCount < teamSize)
            {
                _mainMenuManager.mmmn.JoinTeam(player.FullName, preferredTeamId);
                if (preferredTeamId == 0) team1Count++;
                else team2Count++;
                unassignedPlayers.Remove(player);
            }
            else if (fallbackTeamCount < teamSize)
            {
                _mainMenuManager.mmmn.JoinTeam(player.FullName, fallbackTeamId);
                if (fallbackTeamId == 0) team1Count++;
                else team2Count++;
                unassignedPlayers.Remove(player);
            }
        }

        // Any players who couldn't be assigned (because teams were full) are moved to unassigned.
        foreach (var player in unassignedPlayers)
        {
            _log?.LogInfo($"Balancing: Player '{player.FullName}' could not be assigned (teams full). Removing from team.");
            _mainMenuManager.mmmn.LeftInLobby(player.FullName);
        }
    }

    /// <summary>
    ///     Checks if all players in the lobby have marked themselves as ready.
    /// </summary>
    /// <returns><c>true</c> if all players are ready, or if the check is not applicable; otherwise, <c>false</c>.</returns>
    public bool AreAllPlayersReady()
    {
        if (!IsHost()) return true; // Only the host enforces the ready check.
        // Ignore the check for lobbies with only one player.
        return AllPlayers.Count <= 1 || AllPlayers.All(p => p.IsReady);
    }

    /// <summary>
    ///     Sets the ready status for the local player and broadcasts it to other players.
    /// </summary>
    public void SetLocalPlayerReady(bool isReady)
    {
        var sessionManager = SessionManager.Instance;
        if (sessionManager?.Comms?.LocalPlayerName == null) return;

        var localSteamId = SteamUser.GetSteamID().ToString();
        var player = AllPlayers.FirstOrDefault(p => p.SteamId == localSteamId);
        if (player == null) return;

        player.IsReady = isReady;

        // Broadcast the state change to everyone else.
        sessionManager.SendCommand(new SetReadyStateCommand { Payload = $"{localSteamId};{isReady}" });
    }

    private string ClampString(string input, int maxLength)
    {
        return input.Length <= maxLength ? input : input.Substring(0, maxLength);
    }

    /// <summary>
    ///     Removes a player from the lobby state based on their Steam ID.
    /// </summary>
    public void RemovePlayerBySteamId(string steamId)
    {
        var playerToRemove = AllPlayers.FirstOrDefault(p => p.SteamId == steamId);
        if (playerToRemove == null)
        {
            _log?.LogDebug($"Attempted to remove player with Steam ID '{steamId}' but they were not found. This can be normal.");
            return;
        }

        AllPlayers.Remove(playerToRemove);
        ClearPlayerFromAllTeams(playerToRemove.SteamId!);
        _log?.LogInfo($"Player '{playerToRemove.FullName}' ({playerToRemove.SteamId}) removed from the lobby state via SteamID.");
    }
}
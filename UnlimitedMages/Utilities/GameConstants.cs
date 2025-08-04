using UnityEngine;

namespace UnlimitedMages.Utilities;

/// <summary>
///     A static class containing constant values and magic strings used throughout the mod.
///     This centralizes constants to improve maintainability and reduce fragility from game updates.
/// </summary>
internal static class GameConstants
{
    /// <summary>
    ///     Constants related to general game mechanics.
    /// </summary>
    internal static class Game
    {
        /// <summary>The default team size in the unmodded game.</summary>
        internal const int OriginalTeamSize = 4;

        /// <summary>The number of teams in a standard match.</summary>
        internal const int NumTeams = 2;

        /// <summary>The maximum team size supported by the mod's UI and logic.</summary>
        internal const int MaxTeamSize = 128;

        /// <summary>The minimum team size allowed by the mod.</summary>
        internal const int MinimumTeamSize = 2;
    }

    /// <summary>
    ///     Constants related to the <see cref="global::BootstrapManager" /> class.
    /// </summary>
    internal static class BootstrapManager
    {
        /// <summary>The name of the `OnGetLobbyList` Steam callback method.</summary>
        internal const string OnGetLobbyListMethod = "OnGetLobbyList";

        /// <summary>The name of the `OnLobbyEntered` Steam callback method.</summary>
        internal const string OnLobbyEnteredMethod = "OnLobbyEntered";

        /// <summary>The name of the `ChangeSceneAfterCleanup` coroutine method.</summary>
        internal const string ChangeSceneAfterCleanupMethod = "ChangeSceneAfterCleanup";

        /// <summary>The name of the `OnGetLobbyData` Steam callback method.</summary>
        internal const string OnGetLobbyData = "OnGetLobbyData";

        /// <summary>The name of the private `hasLeaveGameFinished` boolean field.</summary>
        internal const string HasLeaveGameFinishedField = "hasLeaveGameFinished";

        /// <summary>The name of the `OnLobbyCreated` Steam callback method.</summary>
        internal const string OnLobbyCreatedMethod = "OnLobbyCreated";

        /// <summary>The name of the `Awake` Unity message method.</summary>
        internal const string AwakeMethod = "Awake";

        /// <summary>The name of the `OnLobbyChatUpdate` Steam callback method.</summary>
        internal const string OnLobbyChatUpdateMethod = "OnLobbyChatUpdate";

        /// <summary>Gets the version string used to tag and identify modded lobbies.</summary>
        internal static string GetModdedVersionString() => Application.version + "-UM";
    }

    /// <summary>
    ///     Constants related to the Dissonance voice chat integration.
    /// </summary>
    internal static class Dissonance
    {
        /// <summary>The name of the private `stopit` method in `DissonanceFishNetComms`.</summary>
        internal const string StopItMethod = "stopit";
    }

    /// <summary>
    ///     Constants related to the <see cref="global::MainMenuManager" /> class.
    /// </summary>
    internal static class MainMenuManager
    {
        /// <summary>The name of the `Start` Unity message method.</summary>
        internal const string StartMethod = "Start";

        /// <summary>The name of the `SyncHats` RPC method.</summary>
        internal const string SyncHatsMethod = "SyncHats";

        /// <summary>The name of the private `ClampString` helper method.</summary>
        internal const string ClampStringMethod = "ClampString";

        /// <summary>The name of the `RemoveHat` RPC method.</summary>
        internal const string RemoveHatMethod = "RemoveHat";

        /// <summary>The name of the private `bodies` field.</summary>
        internal const string BodiesField = "bodies";

        /// <summary>The name of the private `playerNames` field.</summary>
        internal const string PlayerNamesField = "playerNames";

        /// <summary>The name of the private `playerLevelandRanks` field.</summary>
        internal const string PlayerLevelAndRanksField = "playerLevelandRanks";

        /// <summary>The maximum length for player names in the game's UI.</summary>
        internal const int PlayerNameClampLength = 10;

        /// <summary>The name of the private `lobbyScreen` GameObject field.</summary>
        internal const string LobbyScreenField = "lobbyScreen";

        /// <summary>The name of the private `InGameLobby` GameObject field.</summary>
        internal const string InGameLobbyField = "InGameLobby";
    }

    /// <summary>
    ///     Constants related to the <see cref="global::MainMenuManagerNetworked" /> class.
    /// </summary>
    internal static class MainMenuManagerNetworked
    {
        /// <summary>The name of the `Start` Unity message method.</summary>
        internal const string StartMethod = "Start";

        /// <summary>The name of the `ResetLocalTeam` method.</summary>
        internal const string ResetLocalTeamMethod = "ResetLocalTeam";

        /// <summary>The name of the private `team1players` array field.</summary>
        internal const string Team1PlayersField = "team1players";

        /// <summary>The name of the private `team2players` array field.</summary>
        internal const string Team2PlayersField = "team2players";

        /// <summary>The name of the private `mmm` field (reference to MainMenuManager).</summary>
        internal const string MmmField = "mmm";

        /// <summary>The name of the private `localplayername` string field.</summary>
        internal const string LocalPlayerNameField = "localplayername";

        /// <summary>The name of the private `currentLocalTeam` int field.</summary>
        internal const string CurrentLocalTeamField = "currentLocalTeam";

        /// <summary>The mangled name of the `ObserversJoinTeam` RPC logic method.</summary>
        internal const string ObserversJoinTeamRpc = "RpcLogic___ObserversJoinTeam_964249301";

        /// <summary>The mangled name of the `ObsRemoveFromTeam` RPC logic method.</summary>
        internal const string ObsRemoveFromTeamRpc = "RpcLogic___ObsRemoveFromTeam_1692629761";
    }

    /// <summary>
    ///     Constants related to the <see cref="global::PlayerRespawnManager" /> class.
    /// </summary>
    internal static class PlayerRespawnManager
    {
        /// <summary>The name of the `OnStartClient` method.</summary>
        internal const string OnStartClientMethod = "OnStartClient";

        /// <summary>The name of the `FadeInVignette` coroutine method.</summary>
        internal const string FadeInVignetteMethod = "FadeInVignette";

        /// <summary>The name of the `EndGame` method.</summary>
        internal const string EndGameMethod = "EndGame";

        /// <summary>The name of the `ServerPAsound` RPC method.</summary>
        internal const string ServerPAsoundMethod = "ServerPAsound";

        /// <summary>The name of the `PlayAnnouncerSound` method.</summary>
        internal const string PlayAnnouncerSoundMethod = "PlayAnnouncerSound";

        /// <summary>The name of the `scoreb` scoreboard GameObject field.</summary>
        internal const string ScoreboardField = "scoreb";

        /// <summary>The name of the `positions` float array field for death message UI.</summary>
        internal const string PositionsField = "positions";

        /// <summary>The name of the `warlocksset` int field for team size.</summary>
        internal const string WarlocksSetField = "warlocksset";

        /// <summary>The name of the `DeadPlayers` list field.</summary>
        internal const string DeadPlayersField = "DeadPlayers";

        /// <summary>The name of the private `iscolosseum` boolean field.</summary>
        internal const string IsColosseumField = "iscolosseum";

        /// <summary>The starting Y position for the death message UI list.</summary>
        internal const float DeathMessageFirstY = 449f;

        /// <summary>The ending Y position for the death message UI list.</summary>
        internal const float DeathMessageLastY = 31.2f;
    }

    /// <summary>
    ///     Constants related to the <see cref="global::KickPlayersHolder" /> class.
    /// </summary>
    internal static class KickPlayersHolder
    {
        /// <summary>The name of the `nametosteamid` dictionary field.</summary>
        internal const string NameToSteamIdField = "nametosteamid";

        /// <summary>The name of the `AddToDict` method.</summary>
        internal const string AddToDictMethod = "AddToDict";
    }

    /// <summary>
    ///     Constants related to the mod's custom networking.
    /// </summary>
    internal static class Networking
    {
        /// <summary>The interval in seconds to search for the Dissonance instance.</summary>
        internal const float DissonanceSearchInterval = 1f;

        /// <summary>The prefix used to identify all mod commands sent via chat.</summary>
        internal const string CommandPrefix = "[UNLIMITED_MAGES]";

        /// <summary>The delimiter used to separate the command name from its payload.</summary>
        internal const char CommandDelimiter = ':';
    }
}
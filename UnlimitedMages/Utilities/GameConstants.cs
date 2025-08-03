using UnityEngine;

namespace UnlimitedMages.Utilities;

/// <summary>
///     Provides centralized constants for magic strings and original game values.
///     This improves maintainability by avoiding hardcoded values throughout the patch code.
/// </summary>
internal static class GameConstants
{
    /// <summary>
    ///     Constants related to general game mechanics.
    /// </summary>
    internal static class Game
    {
        /// <summary>
        ///     The original hardcoded size of each team. Used as a baseline for UI calculations and logic patches.
        /// </summary>
        internal const int OriginalTeamSize = 4;

        /// <summary>
        ///     The number of teams in a standard match.
        /// </summary>
        internal const int NumTeams = 2;

        /// <summary>
        ///     A hard upper limit, just in case.
        /// </summary>
        internal const int MaxTeamSize = 128;

        /// <summary>
        ///     The absolute minimum size a team can be. Used for the UI slider.
        /// </summary>
        internal const int MinimumTeamSize = 2;
    }

    /// <summary>
    ///     Constants for patching the BootstrapManager class.
    /// </summary>
    internal static class BootstrapManager
    {
        /// <summary>The name of the method handling lobby list retrieval callbacks.</summary>
        internal const string OnGetLobbyListMethod = "OnGetLobbyList";

        /// <summary>The name of the method handling lobby entry callbacks.</summary>
        internal const string OnLobbyEnteredMethod = "OnLobbyEntered";

        /// <summary>The name of the coroutine responsible for cleaning up network state before a scene change.</summary>
        internal const string ChangeSceneAfterCleanupMethod = "ChangeSceneAfterCleanup";

        /// <summary>A method used by the game to fetch the latest information about a lobby and decide if you're allowed to connect.</summary>
        internal const string OnGetLobbyData = "OnGetLobbyData";


        internal const string HasLeaveGameFinishedField = "hasLeaveGameFinished";

        /// <summary></summary>
        internal const string OnLobbyCreatedMethod = "OnLobbyCreated";

        internal static string GetModdedVersionString() => Application.version + "-UM";
    }

    /// <summary>
    ///     Constants for patching the MainMenuManager class.
    /// </summary>
    internal static class MainMenuManager
    {
        /// <summary>The name of the Start method.</summary>
        internal const string StartMethod = "Start";

        /// <summary>The name of the method that syncs player models/hats in the lobby.</summary>
        internal const string SyncHatsMethod = "SyncHats";

        /// <summary>The name of the utility method for clamping string length.</summary>
        internal const string ClampStringMethod = "ClampString";

        /// <summary>The name of the method that removes a player's model/hat from the lobby.</summary>
        internal const string RemoveHatMethod = "RemoveHat";

        /// <summary>The name of the field containing player character model GameObjects.</summary>
        internal const string BodiesField = "bodies";

        /// <summary>The name of the field containing player names as strings.</summary>
        internal const string PlayerNamesField = "playerNames";

        /// <summary>The name of the field containing player level and rank strings.</summary>
        internal const string PlayerLevelAndRanksField = "playerLevelandRanks";

        /// <summary>The original length to which player names are clamped in the UI.</summary>
        internal const int PlayerNameClampLength = 10;
    }

    /// <summary>
    ///     Constants for patching the MainMenuManagerNetworked class.
    /// </summary>
    internal static class MainMenuManagerNetworked
    {
        /// <summary>The name of the Start method.</summary>
        internal const string StartMethod = "Start";

        /// <summary>The name of the method that resets local team data.</summary>
        internal const string ResetLocalTeamMethod = "ResetLocalTeam";

        /// <summary>The name of the field for the array of player names on team 1.</summary>
        internal const string Team1PlayersField = "team1players";

        /// <summary>The name of the field for the array of player names on team 2.</summary>
        internal const string Team2PlayersField = "team2players";
    }

    /// <summary>
    ///     Constants for patching the PlayerRespawnManager class.
    /// </summary>
    internal static class PlayerRespawnManager
    {
        /// <summary>The name of the OnStartClient method.</summary>
        internal const string OnStartClientMethod = "OnStartClient";

        /// <summary>The name of the coroutine that fades in the death vignette.</summary>
        internal const string FadeInVignetteMethod = "FadeInVignette";

        /// <summary>The name of the method that handles the end-of-game sequence.</summary>
        internal const string EndGameMethod = "EndGame";

        /// <summary>The name of the server RPC that triggers an announcer sound.</summary>
        internal const string ServerPAsoundMethod = "ServerPAsound";

        /// <summary>The name of the method that determines which announcer sound to play.</summary>
        internal const string PlayAnnouncerSoundMethod = "PlayAnnouncerSound";

        /// <summary>The name of the field for the end-game scoreboard GameObject.</summary>
        internal const string ScoreboardField = "scoreb";

        /// <summary>The name of the field for the array of Y-positions for death messages.</summary>
        internal const string PositionsField = "positions";

        /// <summary>The name of the field storing the number of warlocks set for the match.</summary>
        internal const string WarlocksSetField = "warlocksset";

        /// <summary>The name of the field for the list of dead player GameObjects.</summary>
        internal const string DeadPlayersField = "DeadPlayers";

        /// <summary>The starting Y-position for the first death message in the kill feed.</summary>
        internal const float DeathMessageFirstY = 449f;

        /// <summary>The ending Y-position for the last death message in a full 8-player lobby.</summary>
        internal const float DeathMessageLastY = 31.2f;
    }

    /// <summary>
    ///     Constants for all networking-related constants.
    /// </summary>
    internal static class Networking
    {
        /// <summary>The debouncing period between searches of Dissonance manager instances.</summary>
        internal const float DissonanceSearchInterval = 1f;

        /// <summary>The prefix that is expected in the Dissonance communications.</summary>
        internal const string CommandPrefix = "[UNLIMITED_MAGES]";

        /// <summary>The expected command delimiter.</summary>
        internal const char CommandDelimiter = ':';
    }
}
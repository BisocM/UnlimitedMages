namespace UnlimitedMages.UI.Lobby;

/// <summary>
///     A data class that stores all relevant information about a player in the lobby.
/// </summary>
internal class PlayerLobbyData(string fullName, string clampedName, string rank, string? steamId)
{
    /// <summary>
    ///     The player's full, unclamped name.
    /// </summary>
    public string FullName { get; } = fullName;

    /// <summary>
    ///     The player's name, clamped to the game's display length limit.
    /// </summary>
    public string ClampedName { get; } = clampedName;

    /// <summary>
    ///     The player's rank and level string (e.g., "Savant lvl 25").
    /// </summary>
    public string Rank { get; internal set; } = rank;

    /// <summary>
    ///     The player's Steam ID as a string.
    /// </summary>
    public string? SteamId { get; } = steamId;

    /// <summary>
    ///     A value indicating whether the player has marked themselves as ready.
    /// </summary>
    public bool IsReady { get; set; }
}
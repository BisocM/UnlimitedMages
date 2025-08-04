using UnlimitedMages.System.Commands.Attributes;
using UnlimitedMages.System.Components;

namespace UnlimitedMages.System.Commands.Types;

/// <summary>
///     A command sent by the host to a client to synchronize information about a single player in the lobby.
/// </summary>
[ChatCommand("SYNC_PLAYER")]
internal class SyncPlayerCommand : ICommand
{
    /// <summary>
    ///     Gets or sets the payload, formatted as "targetClientId;playerName;playerRank;playerSteamId".
    /// </summary>
    public string Payload { get; set; } = "";
}

/// <summary>
///     Handles the <see cref="SyncPlayerCommand" /> on the client.
///     Parses the payload and adds the player to the local <see cref="LobbyStateManager" />.
/// </summary>
internal class SyncPlayerCommandHandler : ICommandHandler<SyncPlayerCommand>
{
    public void Handle(SyncPlayerCommand command, string senderId)
    {
        var parts = command.Payload.Split(';');
        if (parts.Length != 4) return;

        // Ensure this command is intended for the local player.
        var localPlayerId = SessionManager.Instance?.Comms?.LocalPlayerName;
        if (string.IsNullOrEmpty(localPlayerId) || parts[0] != localPlayerId) return;

        LobbyStateManager.Instance?.AddPlayer(parts[1], parts[2], parts[3]);
    }
}
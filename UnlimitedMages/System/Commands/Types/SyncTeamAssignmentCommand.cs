using UnlimitedMages.System.Commands.Attributes;
using UnlimitedMages.System.Components;

namespace UnlimitedMages.System.Commands.Types;

/// <summary>
///     A command sent by the host to a client to synchronize a player's assignment to a specific team and slot.
/// </summary>
[ChatCommand("SYNC_TEAM_ASSIGNMENT")]
internal class SyncTeamAssignmentCommand : ICommand
{
    /// <summary>
    ///     Gets or sets the payload, formatted as "targetClientId;playerName;teamId;slotIndex;playerRank".
    /// </summary>
    public string Payload { get; set; } = "";
}

/// <summary>
///     Handles the <see cref="SyncTeamAssignmentCommand" /> on the client.
///     Parses the payload and assigns the player to the correct team and slot in the local <see cref="LobbyStateManager" />.
/// </summary>
internal class SyncTeamAssignmentCommandHandler : ICommandHandler<SyncTeamAssignmentCommand>
{
    public void Handle(SyncTeamAssignmentCommand command, string senderId)
    {
        var parts = command.Payload.Split(';');
        if (parts.Length != 5) return;

        // Ensure this command is intended for the local player.
        var localPlayerId = SessionManager.Instance?.Comms?.LocalPlayerName;
        if (string.IsNullOrEmpty(localPlayerId) || parts[0] != localPlayerId) return;

        if (!int.TryParse(parts[2], out var teamId) || !int.TryParse(parts[3], out var slot)) return;

        LobbyStateManager.Instance?.AssignPlayerToTeam(teamId, slot, parts[1], parts[4]);
    }
}
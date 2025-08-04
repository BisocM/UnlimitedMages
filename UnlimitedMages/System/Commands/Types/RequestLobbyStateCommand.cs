using UnlimitedMages.System.Commands.Attributes;
using UnlimitedMages.System.Components;

namespace UnlimitedMages.System.Commands.Types;

/// <summary>
///     A command sent by a client to the host to request a full synchronization of the lobby state.
///     This is typically sent after the client has received the initial configuration.
/// </summary>
[ChatCommand("REQUEST_LOBBY_STATE")]
internal class RequestLobbyStateCommand : ICommand
{
}

/// <summary>
///     Handles the <see cref="RequestLobbyStateCommand" /> on the host.
///     It iterates through all known players and team assignments and sends targeted sync commands back to the requesting client.
/// </summary>
internal class RequestLobbyStateCommandHandler : ICommandHandler<RequestLobbyStateCommand>
{
    public void Handle(RequestLobbyStateCommand command, string senderId)
    {
        var sessionManager = SessionManager.Instance;
        var stateManager = LobbyStateManager.Instance;
        if (sessionManager == null || stateManager == null || !sessionManager.IsHost()) return;

        UnlimitedMagesPlugin.Log?.LogInfo($"Received lobby state request from player {senderId}. Broadcasting full state sync targeted to them.");

        // The payload of sync commands is formatted to include the recipient's ID, so only they process it.

        // Send a command for each player in the lobby.
        foreach (var player in stateManager.AllPlayers)
            sessionManager.SendCommand(new SyncPlayerCommand { Payload = $"{senderId};{player.FullName};{player.Rank};{player.SteamId}" });

        // Send a command for each player assigned to a team slot.
        foreach (var teamEntry in stateManager.Teams)
        {
            var teamId = teamEntry.Key;
            for (var i = 0; i < teamEntry.Value.Length; i++)
            {
                var player = teamEntry.Value[i];
                if (player != null) sessionManager.SendCommand(new SyncTeamAssignmentCommand { Payload = $"{senderId};{player.FullName};{teamId};{i};{player.Rank}" });
            }
        }

        // Send a final command to notify the client that the sync is complete.
        sessionManager.SendCommand(new SyncCompleteCommand { Payload = senderId });
    }
}
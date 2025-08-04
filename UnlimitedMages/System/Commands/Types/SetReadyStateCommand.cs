using UnlimitedMages.System.Commands.Attributes;
using UnlimitedMages.System.Components;

namespace UnlimitedMages.System.Commands.Types;

/// <summary>
///     A command to broadcast a player's ready status to all other players in the lobby.
/// </summary>
[ChatCommand("SET_READY_STATE")]
internal class SetReadyStateCommand : ICommand
{
    /// <summary>
    ///     Gets or sets the payload, formatted as "steamId;isReady".
    /// </summary>
    public string Payload { get; set; } = "";
}

/// <summary>
///     Handles the <see cref="SetReadyStateCommand" />.
///     It parses the payload and updates the ready status of the specified player in the <see cref="LobbyStateManager" />.
/// </summary>
internal class SetReadyStateCommandHandler : ICommandHandler<SetReadyStateCommand>
{
    public void Handle(SetReadyStateCommand command, string senderId)
    {
        var parts = command.Payload.Split(';');
        if (parts.Length != 2) return;

        var steamId = parts[0];
        if (!bool.TryParse(parts[1], out var isReady)) return;

        var stateManager = LobbyStateManager.Instance;
        if (stateManager == null) return;

        // Find the player by Steam ID and update their ready state.
        var player = stateManager.AllPlayers.Find(p => p.SteamId == steamId);
        if (player != null) player.IsReady = isReady;
    }
}
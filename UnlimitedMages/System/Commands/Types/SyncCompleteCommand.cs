using UnlimitedMages.System.Commands.Attributes;
using UnlimitedMages.System.Components;

namespace UnlimitedMages.System.Commands.Types;

/// <summary>
///     A command sent from the host to a specific client to signal that the initial lobby state synchronization is complete.
/// </summary>
[ChatCommand("SYNC_COMPLETE")]
internal class SyncCompleteCommand : ICommand
{
    /// <summary>
    ///     Gets or sets the payload, which contains the network ID of the targeted client.
    /// </summary>
    public string Payload { get; set; } = "";
}

/// <summary>
///     Handles the <see cref="SyncCompleteCommand" /> on the client.
///     It sets a flag in the <see cref="SessionManager" /> to indicate that the lobby state is now synchronized.
/// </summary>
internal class SyncCompleteCommandHandler : ICommandHandler<SyncCompleteCommand>
{
    public void Handle(SyncCompleteCommand command, string senderId)
    {
        var sessionManager = SessionManager.Instance;
        if (sessionManager == null) return;

        // Ensure this command is for me.
        var localPlayerId = sessionManager.Comms?.LocalPlayerName;
        if (string.IsNullOrEmpty(localPlayerId) || command.Payload != localPlayerId) return;

        sessionManager.LobbyStateSynced = true;
        UnlimitedMagesPlugin.Log?.LogInfo("Initial lobby state sync complete.");
    }
}
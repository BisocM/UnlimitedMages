using UnlimitedMages.System.Commands.Attributes;
using UnlimitedMages.System.Components;

namespace UnlimitedMages.System.Commands.Types;

/// <summary>
///     A command sent from the host to all clients to inform them of the chosen team size for the session.
/// </summary>
[ChatCommand("SET_TEAM_SIZE")]
internal class SetTeamSizeCommand : ICommand
{
    /// <summary>
    ///     Gets or sets the payload, which contains the integer value of the team size.
    /// </summary>
    public string Payload { get; set; } = "";
}

/// <summary>
///     Handles the <see cref="SetTeamSizeCommand" /> on the client.
///     It finalizes the client's local configuration, which in turn triggers events to resize UI and game arrays.
/// </summary>
internal class SetTeamSizeCommandHandler : ICommandHandler<SetTeamSizeCommand>
{
    public void Handle(SetTeamSizeCommand command, string senderId)
    {
        var sessionManager = SessionManager.Instance;
        var configManager = ConfigManager.Instance;

        // Ignore if this is the host or if config is already set.
        if (sessionManager == null || configManager == null || configManager.IsConfigReady) return;

        if (!int.TryParse(command.Payload, out var newSize)) return;

        UnlimitedMagesPlugin.Log?.LogInfo($"[Client] Received team size from host: {newSize}. Finalizing config.");
        configManager.FinalizeConfig(newSize);
    }
}
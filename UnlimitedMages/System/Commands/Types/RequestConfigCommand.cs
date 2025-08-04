using UnlimitedMages.System.Commands.Attributes;
using UnlimitedMages.System.Components;

namespace UnlimitedMages.System.Commands.Types;

/// <summary>
///     A command sent by a client to the host to request the current session configuration (e.g., team size).
/// </summary>
[ChatCommand("REQUEST_CONFIG")]
internal class RequestConfigCommand : ICommand
{
}

/// <summary>
///     Handles the <see cref="RequestConfigCommand" /> on the host.
///     Responds to the client with a <see cref="SetTeamSizeCommand" /> containing the current configuration.
/// </summary>
internal class RequestConfigCommandHandler : ICommandHandler<RequestConfigCommand>
{
    public void Handle(RequestConfigCommand command, string senderId)
    {
        var sessionManager = SessionManager.Instance;
        var configManager = ConfigManager.Instance;
        if (sessionManager == null || configManager == null || !sessionManager.IsHost()) return;

        UnlimitedMagesPlugin.Log?.LogInfo($"Received config request from {senderId}. Sending current team size.");
        sessionManager.SendCommand(new SetTeamSizeCommand { Payload = configManager.TeamSize.ToString() });
    }
}
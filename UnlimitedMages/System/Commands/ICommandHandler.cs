namespace UnlimitedMages.System.Commands;

/// <summary>
///     Defines a handler for a specific type of command.
/// </summary>
/// <typeparam name="TCommand">The type of <see cref="ICommand" /> this handler can process.</typeparam>
internal interface ICommandHandler<in TCommand> where TCommand : ICommand
{
    /// <summary>
    ///     Handles the execution of the command.
    /// </summary>
    /// <param name="command">The command instance to handle.</param>
    /// <param name="senderId">The network identifier of the player who sent the command.</param>
    void Handle(TCommand command, string senderId);
}
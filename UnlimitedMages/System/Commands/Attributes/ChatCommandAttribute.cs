using System;

namespace UnlimitedMages.System.Commands.Attributes;

/// <summary>
///     An attribute used to associate a command class with a specific chat command string.
///     This allows the <see cref="CommandDispatcher" /> to map incoming text commands to the correct handler.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal class ChatCommandAttribute(string commandName) : Attribute
{
    /// <summary>
    ///     Gets the name of the command (e.g., "SET_TEAM_SIZE").
    /// </summary>
    public string CommandName { get; } = commandName;
}
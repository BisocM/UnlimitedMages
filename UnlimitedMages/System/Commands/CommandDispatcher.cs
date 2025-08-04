using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnlimitedMages.System.Commands.Attributes;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.System.Commands;

/// <summary>
///     Discovers and dispatches chat-based commands to their corresponding handlers.
///     Uses reflection to find all command handlers at startup.
/// </summary>
internal class CommandDispatcher
{
    private readonly Dictionary<string, (Type commandType, Type handlerType)> _commandMap = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommandDispatcher" /> class
    ///     and discovers all available command handlers.
    /// </summary>
    public CommandDispatcher()
    {
        DiscoverHandlers();
    }

    /// <summary>
    ///     Scans the executing assembly for all classes that implement <see cref="ICommandHandler{TCommand}" />
    ///     and registers them in the command map.
    /// </summary>
    private void DiscoverHandlers()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var handlerTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.GetInterfaces()
                .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandler<>)));

        foreach (var handlerType in handlerTypes)
        {
            // Determine the command type from the generic interface implementation.
            var commandType = handlerType.GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICommandHandler<>))
                .GetGenericArguments()[0];

            // Get the command name from the [ChatCommand] attribute on the command class.
            var attribute = commandType.GetCustomAttribute<ChatCommandAttribute>();
            if (attribute != null)
            {
                var commandKey = $"{GameConstants.Networking.CommandPrefix}{attribute.CommandName}";
                _commandMap[commandKey] = (commandType, handlerType);
                UnlimitedMagesPlugin.Log?.LogInfo($"[CommandDispatcher] Mapped command '{commandKey}' to handler '{handlerType.Name}'.");
            }
        }
    }

    /// <summary>
    ///     Parses a raw message string, identifies the command, creates an instance of it,
    ///     and invokes the corresponding handler.
    /// </summary>
    /// <param name="rawMessage">The full message received, including the prefix and payload.</param>
    /// <param name="senderId">The network identifier of the player who sent the command.</param>
    public void Dispatch(string rawMessage, string senderId)
    {
        var parts = rawMessage.Split([GameConstants.Networking.CommandDelimiter], 2);
        var commandKey = parts[0];

        if (!_commandMap.TryGetValue(commandKey, out var types)) return;

        var (commandType, handlerType) = types;

        try
        {
            // Create an instance of the command and its handler.
            var command = (ICommand)Activator.CreateInstance(commandType);

            // If there's a payload, find the 'Payload' property on the command and set its value.
            if (parts.Length > 1)
            {
                var payloadProperty = commandType.GetProperties().FirstOrDefault();
                payloadProperty?.SetValue(command, parts[1]);
            }

            // Invoke the Handle method on the handler instance.
            var handler = Activator.CreateInstance(handlerType);
            handlerType.GetMethod("Handle")?.Invoke(handler, [command, senderId]);
        }
        catch (Exception ex)
        {
            UnlimitedMagesPlugin.Log?.LogError($"Error dispatching command '{commandKey}': {ex}");
        }
    }
}
using System;
using System.Collections.Generic;

namespace UnlimitedMages.System.Events;

/// <summary>
///     A simple static event bus for decoupled communication between different mod systems.
/// </summary>
internal static class EventBus
{
    private static readonly Dictionary<Type, Delegate> SEvents = new();

    /// <summary>
    ///     Subscribes a listener to a specific event type.
    /// </summary>
    /// <typeparam name="T">The type of the event to listen for.</typeparam>
    /// <param name="listener">The action to execute when the event is published.</param>
    public static void Subscribe<T>(Action<T> listener)
    {
        var eventType = typeof(T);
        if (SEvents.TryGetValue(eventType, out var del))
            SEvents[eventType] = Delegate.Combine(del, listener);
        else
            SEvents[eventType] = listener;
    }

    /// <summary>
    ///     Unsubscribes a listener from a specific event type.
    /// </summary>
    /// <typeparam name="T">The type of the event to unsubscribe from.</typeparam>
    /// <param name="listener">The action that was previously subscribed.</param>
    public static void Unsubscribe<T>(Action<T> listener)
    {
        var eventType = typeof(T);
        if (!SEvents.TryGetValue(eventType, out var del)) return;

        var newDel = Delegate.Remove(del, listener);
        if (newDel == null)
            SEvents.Remove(eventType);
        else
            SEvents[eventType] = newDel;
    }

    /// <summary>
    ///     Publishes an event to all subscribed listeners.
    /// </summary>
    /// <typeparam name="T">The type of the event.</typeparam>
    /// <param name="message">The event instance to publish.</param>
    public static void Publish<T>(T message)
    {
        if (SEvents.TryGetValue(typeof(T), out var del))
            del.DynamicInvoke(message);
    }
}
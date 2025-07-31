using System;
using System.Collections.Generic;

namespace UnlimitedMages.System.Events;

public static class EventBus
{
    private static readonly Dictionary<Type, Delegate> SEvents = new();

    /// <summary>
    /// Subscribes a listener to a specific type of event message.
    /// </summary>
    /// <param name="listener">The action to execute when the event is published.</param>
    /// <typeparam name="T">The type of the event message to listen for.</typeparam>
    public static void Subscribe<T>(Action<T> listener)
    {
        var eventType = typeof(T);
        if (SEvents.TryGetValue(eventType, out var del))
            SEvents[eventType] = Delegate.Combine(del, listener);
        else
            SEvents[eventType] = listener;
    }
    
    /// <summary>
    /// Unsubscribes a listener from a specific type of event message.
    /// </summary>
    /// <param name="listener">The action to remove.</param>
    /// <typeparam name="T">The type of the event message to stop listening for.</typeparam>
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
    /// Publishes an event message to all subscribed listeners.
    /// </summary>
    /// <param name="message">The event message object to send.</param>
    /// <typeparam name="T">The type of the event message.</typeparam>
    public static void Publish<T>(T message)
    {
        if (SEvents.TryGetValue(typeof(T), out var del))
            del.DynamicInvoke(message);
    }
}
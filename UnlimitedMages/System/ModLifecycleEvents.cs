using System;

namespace UnlimitedMages.System;

/// <summary>
///     Provides global, static events for key moments in the mod's lifecycle.
/// </summary>
public static class ModLifecycleEvents
{
    /// <summary>
    ///     This event is fired exactly once when the game's BootstrapManager is initialized and ready.
    /// </summary>
    public static event Action? OnBootstrapReady;

    /// <summary>
    ///     Invokes the OnBootstrapReady event. This should only be called by the patch that detects it.
    /// </summary>
    internal static void InvokeBootstrapReady()
    {
        OnBootstrapReady?.Invoke();
    }
}
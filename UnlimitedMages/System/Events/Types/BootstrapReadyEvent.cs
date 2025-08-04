namespace UnlimitedMages.System.Events.Types;

/// <summary>
///     An event published when the game's <see cref="global::BootstrapManager" /> has completed its Awake method.
///     This signals that it's safe to inject the mod's components.
/// </summary>
internal class BootstrapReadyEvent
{
}
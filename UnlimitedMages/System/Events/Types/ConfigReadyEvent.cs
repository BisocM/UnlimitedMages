namespace UnlimitedMages.System.Events.Types;

/// <summary>
///     An event published when the session's configuration has been finalized (either locally as host or received from the host as a client).
/// </summary>
internal class ConfigReadyEvent(int teamSize)
{
    /// <summary>
    ///     The finalized team size for the session.
    /// </summary>
    public readonly int TeamSize = teamSize;
}
namespace UnlimitedMages.System.Events.Types;

/// <summary>
///     Published when the session configuration is finalized, containing the agreed-upon team size.
/// </summary>
internal class ConfigReadyEvent(int teamSize)
{
    public readonly int TeamSize = teamSize;
}
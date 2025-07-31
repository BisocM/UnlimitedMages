namespace UnlimitedMages.System.Events.Types;

/// <summary>
/// Published when the session configuration is finalized, containing the agreed-upon team size.
/// </summary>
public class ConfigReadyEvent(int teamSize)
{
    public readonly int TeamSize = teamSize;
}
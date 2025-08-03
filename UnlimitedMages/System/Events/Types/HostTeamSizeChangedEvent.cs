namespace UnlimitedMages.System.Events.Types;

/// <summary>
///     Published when the host changes the team size via the UI.
/// </summary>
internal class HostTeamSizeChangedEvent(int newTeamSize)
{
    public readonly int NewTeamSize = newTeamSize;
}
namespace UnlimitedMages.System.Events.Types;

/// <summary>
///     An event published when the host changes the team size using the UI slider.
///     This is used to trigger a network broadcast of the new size.
/// </summary>
internal class HostTeamSizeChangedEvent(int newTeamSize)
{
    /// <summary>
    ///     The new team size selected by the host.
    /// </summary>
    public readonly int NewTeamSize = newTeamSize;
}
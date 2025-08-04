namespace UnlimitedMages.UI.Popup;

/// <summary>
///     Enumerates the types of buttons that can be displayed on a popup, used for styling.
/// </summary>
internal enum PopupButton
{
    /// <summary>
    ///     A standard confirmation button (e.g., "OK", "Confirm").
    /// </summary>
    Ok,

    /// <summary>
    ///     A button indicating caution or a non-critical choice (e.g., "Wait", "Cancel").
    /// </summary>
    Warning,

    /// <summary>
    ///     A button for critical or destructive actions (e.g., "Abort", "Force Start").
    /// </summary>
    Error
}
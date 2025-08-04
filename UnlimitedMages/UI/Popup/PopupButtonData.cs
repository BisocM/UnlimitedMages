namespace UnlimitedMages.UI.Popup;

/// <summary>
///     A struct that holds the data required to define a button on a popup window.
/// </summary>
/// <param name="type">The button type, which determines its styling.</param>
/// <param name="text">The text to display on the button.</param>
internal readonly struct PopupButtonData(PopupButton type, string text)
{
    /// <summary>
    ///     The button type, which determines its styling.
    /// </summary>
    public PopupButton Type { get; } = type;

    /// <summary>
    ///     The text to display on the button.
    /// </summary>
    public string Text { get; } = text;
}
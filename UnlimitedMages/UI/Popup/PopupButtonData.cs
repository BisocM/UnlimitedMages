namespace UnlimitedMages.UI.Popup;

internal readonly struct PopupButtonData(PopupButton type, string text)
{
    public PopupButton Type { get; } = type;
    public string Text { get; } = text;
}
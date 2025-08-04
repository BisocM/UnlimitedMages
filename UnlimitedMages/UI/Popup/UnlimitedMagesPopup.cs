using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace UnlimitedMages.UI.Popup;

/// <summary>
///     A MonoBehaviour that renders a generic, modal popup window using Unity's immediate mode GUI.
/// </summary>
internal sealed class UnlimitedMagesPopup : MonoBehaviour
{
    private List<PopupButtonData> _buttons = new();
    private Dictionary<PopupButton, GUIStyle> _buttonStyles = new();
    private bool _isVisible;
    private GUIStyle _labelStyle = new();
    private string _message = "";
    private Action<PopupButton> _onButtonClicked = _ => { };
    private bool _stylesInitialized;
    private string _title = "";
    private Rect _windowRect;
    private GUIStyle _windowStyle = new();

    private void Update()
    {
        if (!_isVisible) return;

        // Keep the cursor visible and unlocked while the popup is active.
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDestroy()
    {
        // Ensure the UI blocker is hidden when the popup is destroyed.
        if (_isVisible) ModUIManager.Instance?.OnPopupClosed();
    }

    private void OnGUI()
    {
        if (!_isVisible) return;
        InitializeStyles();

        // Center the window on the screen.
        _windowRect.x = (Screen.width - _windowRect.width) / 2;
        _windowRect.y = (Screen.height - _windowRect.height) / 2;

        GUI.ModalWindow(0, _windowRect, DrawWindow, _title, _windowStyle);
    }

    /// <summary>
    ///     Configures and displays the popup with the specified content and buttons.
    /// </summary>
    public void Configure(string title, string message, Action<PopupButton> onButtonClicked, params PopupButtonData[] buttons)
    {
        _title = title;
        _message = message;
        _onButtonClicked = onButtonClicked;

        _buttons = buttons.Length > 0
            ? buttons.ToList()
            : new List<PopupButtonData> { new(PopupButton.Ok, "OK") };

        _isVisible = true;
        _windowRect = new Rect(0, 0, 600, 320); // Position will be centered in OnGUI.
    }

    /// <summary>
    ///     Closes the popup, invokes the button click callback, and destroys the GameObject.
    /// </summary>
    private void ClosePopup(PopupButton buttonType)
    {
        _isVisible = false;
        _onButtonClicked.Invoke(buttonType);
        ModUIManager.Instance?.OnPopupClosed();
        Destroy(gameObject);
    }

    private static Texture2D CreateSolidColorTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    /// <summary>
    ///     Creates a GUIStyle for a popup button with a specified hover color.
    /// </summary>
    private GUIStyle CreateButtonStyle(Color hoverColor)
    {
        var baseColor = new Color(0.2f, 0.22f, 0.25f, 1.0f);
        return new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter,
            normal = { background = CreateSolidColorTexture(baseColor), textColor = Color.white },
            hover = { background = CreateSolidColorTexture(hoverColor), textColor = Color.white },
            active = { background = CreateSolidColorTexture(hoverColor) },
            border = new RectOffset(0, 0, 0, 0),
            padding = new RectOffset(10, 10, 12, 12),
            margin = new RectOffset(5, 5, 0, 0)
        };
    }

    /// <summary>
    ///     Initializes all GUIStyle objects for the popup. This is only run once.
    /// </summary>
    private void InitializeStyles()
    {
        if (_stylesInitialized) return;

        var windowBgColor = new Color(0.12f, 0.13f, 0.16f, 1.0f);
        var textColor = new Color(0.9f, 0.9f, 0.9f, 1.0f);
        var opaqueBackground = CreateSolidColorTexture(windowBgColor);

        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            normal = { background = opaqueBackground, textColor = Color.white },
            onNormal = { background = opaqueBackground, textColor = Color.white },
            padding = new RectOffset(10, 10, 10, 10),
            border = new RectOffset(2, 2, 2, 2),
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter
        };
        _windowStyle.hover = _windowStyle.onHover = _windowStyle.active = _windowStyle.onActive = _windowStyle.focused = _windowStyle.onFocused = _windowStyle.normal;

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            wordWrap = true,
            richText = true, // Enable rich text for formatting like <b> and <color>.
            alignment = TextAnchor.UpperLeft,
            normal = { textColor = textColor },
            padding = new RectOffset(15, 15, 15, 15)
        };

        _buttonStyles = new Dictionary<PopupButton, GUIStyle>
        {
            [PopupButton.Ok] = CreateButtonStyle(new Color(0.3f, 0.65f, 1.0f, 1.0f)),
            [PopupButton.Warning] = CreateButtonStyle(new Color(1.0f, 0.75f, 0.0f, 1.0f)),
            [PopupButton.Error] = CreateButtonStyle(new Color(0.9f, 0.2f, 0.2f, 1.0f))
        };

        _stylesInitialized = true;
    }

    /// <summary>
    ///     The drawing method for the modal window, passed to GUI.ModalWindow.
    /// </summary>
    private void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();
        GUILayout.Space(22); // Space for the title.
        GUILayout.Label(_message, _labelStyle, GUILayout.ExpandHeight(true));
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        foreach (var button in _buttons.Where(button => GUILayout.Button(button.Text.ToUpper(), _buttonStyles[button.Type], GUILayout.Height(40), GUILayout.Width(120))))
        {
            ClosePopup(button.Type);
            return; // Return early as the component is about to be destroyed.
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        GUILayout.EndVertical();

        GUI.DragWindow();
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace UnlimitedMages.UI.Popup;

internal sealed class UnlimitedMagesPopup : MonoBehaviour
{
    private List<PopupButtonData> _buttons = new();
    private Dictionary<PopupButton, GUIStyle> _buttonStyles = new();
    private GameObject? _inputBlocker;
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
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void OnDestroy()
    {
        if (_inputBlocker != null) Destroy(_inputBlocker);
    }

    private void OnGUI()
    {
        if (!_isVisible) return;
        InitializeStyles();

        _windowRect.x = (Screen.width - _windowRect.width) / 2;
        _windowRect.y = (Screen.height - _windowRect.height) / 2;

        GUI.ModalWindow(0, _windowRect, DrawWindow, _title, _windowStyle);
    }

    public static void Show(string title, string message, Action<PopupButton> onButtonClicked, params PopupButtonData[] buttons)
    {
        if (FindFirstObjectByType<UnlimitedMagesPopup>() != null) return;

        var go = new GameObject("UnlimitedMages_Popup");
        DontDestroyOnLoad(go);

        var alert = go.AddComponent<UnlimitedMagesPopup>();
        alert._title = title;
        alert._message = message;
        alert._onButtonClicked = onButtonClicked;

        // If no buttons are provided, default to a single "OK" button.
        alert._buttons = buttons.Length > 0
            ? buttons.ToList()
            : new List<PopupButtonData> { new(PopupButton.Ok, "OK") };

        alert.CreateInputBlocker();
        alert._isVisible = true;
        alert._windowRect = new Rect((Screen.width - 600) / 2, (Screen.height - 320) / 2, 600, 320);
    }

    private void CreateInputBlocker()
    {
        _inputBlocker = new GameObject("UIPopupBlocker");
        DontDestroyOnLoad(_inputBlocker);

        var canvas = _inputBlocker.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        _inputBlocker.AddComponent<GraphicRaycaster>();

        var blockerPanel = new GameObject("BlockerPanel");
        blockerPanel.transform.SetParent(_inputBlocker.transform, false);
        var panelImage = blockerPanel.AddComponent<Image>();
        panelImage.color = Color.clear;
        panelImage.raycastTarget = true;

        var rectTransform = blockerPanel.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;
    }

    private static Texture2D CreateSolidColorTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

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

    private void InitializeStyles()
    {
        if (_stylesInitialized) return;

        var windowBgColor = new Color(0.12f, 0.13f, 0.16f, 1.0f);
        var textColor = new Color(0.9f, 0.9f, 0.9f, 1.0f);
        var opaqueBackground = CreateSolidColorTexture(windowBgColor);

        _windowStyle = new GUIStyle
        {
            normal = { background = opaqueBackground, textColor = Color.white },
            active = { background = opaqueBackground, textColor = Color.white },
            hover = { background = opaqueBackground, textColor = Color.white },
            focused = { background = opaqueBackground, textColor = Color.white },
            onNormal = { background = opaqueBackground, textColor = Color.white },
            onActive = { background = opaqueBackground, textColor = Color.white },
            onHover = { background = opaqueBackground, textColor = Color.white },
            onFocused = { background = opaqueBackground, textColor = Color.white },
            padding = new RectOffset(10, 10, 10, 10),
            border = new RectOffset(2, 2, 2, 2),
            fontSize = 20,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter
        };

        _labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            wordWrap = true,
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

    private void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();
        GUILayout.Space(22);
        GUILayout.Label(_message, _labelStyle, GUILayout.ExpandHeight(true));
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        foreach (var button in _buttons.Where(button => GUILayout.Button(button.Text.ToUpper(), _buttonStyles[button.Type], GUILayout.Height(40), GUILayout.Width(120))))
        {
            _isVisible = false;
            _onButtonClicked.Invoke(button.Type);
            Destroy(gameObject);
            return;
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        GUILayout.EndVertical();

        GUI.DragWindow();
    }
}
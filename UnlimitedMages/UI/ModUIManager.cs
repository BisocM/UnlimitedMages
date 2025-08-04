using System;
using BepInEx.Logging;
using UnityEngine;
using UnityEngine.UI;
using UnlimitedMages.Components;
using UnlimitedMages.UI.Lobby;
using UnlimitedMages.UI.Popup;

namespace UnlimitedMages.UI;

/// <summary>
///     A singleton component that manages the mod's entire UI hierarchy.
///     It creates a dedicated canvas and provides methods for showing and hiding UI elements like popups.
/// </summary>
internal class ModUIManager : MonoBehaviour, IModComponent
{
    private GameObject? _uiBlocker;

    /// <summary>
    ///     Gets the singleton instance of the ModUIManager.
    /// </summary>
    public static ModUIManager? Instance { get; private set; }

    /// <summary>
    ///     Gets the root GameObject of the mod's UI canvas.
    /// </summary>
    public GameObject? CanvasRoot { get; private set; }

    /// <summary>
    ///     Gets the host GameObject for the custom lobby UI.
    /// </summary>
    public GameObject? LobbyUiHost { get; private set; }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Initialize(ManualLogSource log)
    {
        if (Instance != null)
        {
            log.LogWarning("Duplicate ModUIManager instance detected. Destroying this component.");
            Destroy(this);
            return;
        }

        Instance = this;

        log.LogInfo("Creating dedicated UI canvas for the mod...");

        // Create the main canvas that will hold all mod UI.
        CanvasRoot = new GameObject("UnlimitedMages_UI_Root");
        DontDestroyOnLoad(CanvasRoot);

        var canvas = CanvasRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // High sorting order to render above game UI.

        var scaler = CanvasRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        CanvasRoot.AddComponent<GraphicRaycaster>();

        // Create a blocker panel used to prevent clicks on game UI behind popups.
        _uiBlocker = new GameObject("UI_Blocker_Panel");
        _uiBlocker.transform.SetParent(CanvasRoot.transform, false);
        var blockerImage = _uiBlocker.AddComponent<Image>();
        blockerImage.color = new Color(0, 0, 0, 0.4f);
        blockerImage.raycastTarget = true;
        var blockerRect = _uiBlocker.GetComponent<RectTransform>();
        blockerRect.anchorMin = Vector2.zero;
        blockerRect.anchorMax = Vector2.one;
        blockerRect.sizeDelta = Vector2.zero;
        _uiBlocker.SetActive(false);

        // Create a host object for the OnGUI-based lobby UI.
        LobbyUiHost = new GameObject("LobbyUI_Host");
        LobbyUiHost.transform.SetParent(CanvasRoot.transform, false);
        LobbyUiHost.AddComponent<LobbyUI>();
        LobbyUiHost.SetActive(false);

        log.LogInfo("Mod UI canvas and hierarchy created successfully.");
        CanvasRoot.SetActive(true);
    }

    /// <summary>
    ///     Displays a modal popup window.
    /// </summary>
    /// <param name="title">The title of the popup.</param>
    /// <param name="message">The message body of the popup.</param>
    /// <param name="onButtonClicked">The callback to invoke when a button is clicked.</param>
    /// <param name="buttons">The set of buttons to display on the popup.</param>
    public void ShowPopup(string title, string message, Action<PopupButton> onButtonClicked, params PopupButtonData[] buttons)
    {
        if (CanvasRoot == null) return;

        // Prevent showing multiple popups at once.
        if (CanvasRoot.transform.Find("Popup_Host") != null) return;

        var popupHost = new GameObject("Popup_Host");
        popupHost.transform.SetParent(CanvasRoot.transform, false);

        var popupComponent = popupHost.AddComponent<UnlimitedMagesPopup>();
        popupComponent.Configure(title, message, onButtonClicked, buttons);

        _uiBlocker?.SetActive(true);
    }

    /// <summary>
    ///     Callback method to be called by a popup when it is closed. Hides the UI blocker.
    /// </summary>
    public void OnPopupClosed()
    {
        _uiBlocker?.SetActive(false);
    }
}
using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnlimitedMages.Components;
using UnlimitedMages.System.Components;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.UI.Lobby;

/// <summary>
///     A helper component to track if a slider is currently being manipulated by the user.
/// </summary>
internal class SliderInteractionHelper : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool IsBeingDragged { get; private set; }
    public void OnPointerDown(PointerEventData eventData) => IsBeingDragged = true;
    public void OnPointerUp(PointerEventData eventData) => IsBeingDragged = false;
}

/// <summary>
///     Manages the custom UI slider for selecting the team size.
///     It creates the slider GameObject and handles its visibility and value changes.
/// </summary>
internal sealed class UnlimitedMagesSlider : MonoBehaviour, IModComponent
{
    private const string CreateLobbyPanelName = "CreateLobbyMenu";
    private SliderInteractionHelper? _sliderHelper;
    private TextMeshProUGUI? _sliderLabel;

    private Slider? _teamSizeSlider;

    /// <summary>
    ///     Gets the team size currently selected on the UI slider. This value is used by the host when creating a lobby.
    /// </summary>
    public static int SelectedTeamSize { get; private set; } = GameConstants.Game.MinimumTeamSize;

    /// <summary>
    ///     In the Update loop, checks if the "Create Lobby" panel is active and toggles the slider's visibility accordingly.
    /// </summary>
    private void Update()
    {
        if (_teamSizeSlider == null) return;

        var createLobbyPanel = GameObject.Find(CreateLobbyPanelName);
        var shouldBeActive = createLobbyPanel != null && createLobbyPanel.activeInHierarchy;

        if (_teamSizeSlider.gameObject.activeSelf != shouldBeActive) _teamSizeSlider.gameObject.SetActive(shouldBeActive);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<ConfigReadyEvent>(OnConfigReady_UpdateSlider);
    }

    public void Initialize(ManualLogSource log)
    {
        if (ModUIManager.Instance?.CanvasRoot == null)
        {
            log.LogError("ModUIManager is not ready. Cannot create the team size slider.");
            return;
        }

        CreateSliderObject(ModUIManager.Instance.CanvasRoot.transform, log);
        EventBus.Subscribe<ConfigReadyEvent>(OnConfigReady_UpdateSlider);
        log.LogInfo("Team size slider initialized and parented to custom UI canvas.");
    }

    /// <summary>
    ///     Programmatically creates the slider GameObject and all its child components (Fill, Handle, Label).
    /// </summary>
    private void CreateSliderObject(Transform parent, ManualLogSource log)
    {
        if (parent.Find("LobbySizeSlider") != null) return;
        log.LogInfo($"Injecting team size slider into '{parent.name}'.");

        var bgColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
        var fillColor = new Color(0.3f, 0.65f, 1.0f, 0.9f);
        var handleColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        var sliderObj = new GameObject("LobbySizeSlider");
        sliderObj.transform.SetParent(parent, false);

        _teamSizeSlider = sliderObj.AddComponent<Slider>();
        _sliderHelper = sliderObj.AddComponent<SliderInteractionHelper>();
        sliderObj.AddComponent<Image>().color = bgColor;

        var rect = sliderObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300, 25);
        rect.anchoredPosition = new Vector2(0, -20); // Center-aligned, slightly below center

        var fill = new GameObject("Fill");
        fill.transform.SetParent(rect, false);
        var fillImage = fill.AddComponent<Image>();
        fillImage.color = fillColor;
        _teamSizeSlider.fillRect = fill.GetComponent<RectTransform>();
        _teamSizeSlider.fillRect.anchorMin = new Vector2(0, 0);
        _teamSizeSlider.fillRect.anchorMax = new Vector2(0, 1);
        _teamSizeSlider.fillRect.pivot = new Vector2(0, 0.5f);
        _teamSizeSlider.fillRect.sizeDelta = Vector2.zero;

        var handle = new GameObject("Handle");
        handle.transform.SetParent(rect, false);
        var handleImage = handle.AddComponent<Image>();
        handleImage.color = handleColor;
        _teamSizeSlider.handleRect = handle.GetComponent<RectTransform>();
        _teamSizeSlider.targetGraphic = handleImage;
        _teamSizeSlider.handleRect.sizeDelta = new Vector2(15, 25);

        var textObj = new GameObject("LobbySizeLabel");
        textObj.transform.SetParent(sliderObj.transform, false);
        _sliderLabel = textObj.AddComponent<TextMeshProUGUI>();
        _sliderLabel.fontSize = 20;
        _sliderLabel.color = Color.white;
        _sliderLabel.alignment = TextAlignmentOptions.Center;
        var labelRect = textObj.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 1);
        labelRect.anchorMax = new Vector2(0.5f, 1);
        labelRect.pivot = new Vector2(0.5f, 0);
        labelRect.anchoredPosition = new Vector2(0, 5);
        labelRect.sizeDelta = new Vector2(300, 30);

        _teamSizeSlider.direction = Slider.Direction.LeftToRight;
        _teamSizeSlider.minValue = GameConstants.Game.MinimumTeamSize;
        _teamSizeSlider.maxValue = GameConstants.Game.MaxTeamSize;
        _teamSizeSlider.wholeNumbers = true;
        _teamSizeSlider.value = SelectedTeamSize;
        UpdateSliderDisplay((int)_teamSizeSlider.value);
        _teamSizeSlider.onValueChanged.AddListener(OnSliderValueChanged);

        sliderObj.SetActive(false);
    }

    /// <summary>
    ///     Callback for when the slider's value changes. Updates the static property and fires an event if the host changes the value.
    /// </summary>
    private void OnSliderValueChanged(float value)
    {
        var intValue = (int)value;
        UpdateSliderDisplay(intValue);
        SelectedTeamSize = intValue;

        // If the host changes the value, publish an event so the SessionManager can broadcast it.
        if (ConfigManager.Instance is { IsConfigReady: true })
            EventBus.Publish(new HostTeamSizeChangedEvent(intValue));
    }

    private void UpdateSliderDisplay(int value)
    {
        if (_sliderLabel != null)
            _sliderLabel.text = $"Team Size: {value} vs {value}";
    }

    /// <summary>
    ///     Event handler to update the slider's visual state when a configuration is received from the network.
    ///     This keeps the UI in sync for all players.
    /// </summary>
    private void OnConfigReady_UpdateSlider(ConfigReadyEvent evt)
    {
        // Don't update if the user is currently dragging the slider, to avoid fighting for control.
        if (_sliderLabel == null || _teamSizeSlider == null || (_sliderHelper != null && _sliderHelper.IsBeingDragged)) return;

        UpdateSliderDisplay(evt.TeamSize);
        _teamSizeSlider.SetValueWithoutNotify(evt.TeamSize);
    }
}
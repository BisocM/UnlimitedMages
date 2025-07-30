using BepInEx.Logging;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnlimitedMages.Components;
using UnlimitedMages.Networking;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.UI;

/// <summary>
///     A helper component to detect when a UI slider is being actively dragged by the user.
///     This prevents network updates from overriding the user's input.
/// </summary>
public class SliderInteractionHelper : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    /// <summary>
    ///     Gets a value indicating whether the user is currently dragging the slider handle.
    /// </summary>
    public bool IsBeingDragged { get; private set; }

    /// <summary>
    ///     Sets IsBeingDragged to true when the user presses down on the slider.
    /// </summary>
    public void OnPointerDown(PointerEventData eventData) => IsBeingDragged = true;

    /// <summary>
    ///     Sets IsBeingDragged to false when the user releases the slider.
    /// </summary>
    public void OnPointerUp(PointerEventData eventData) => IsBeingDragged = false;
}

/// <summary>
///     A mod component responsible for injecting a custom team size slider into the
///     "Create Lobby" screen of the main menu.
/// </summary>
public class UISliderInjector : MonoBehaviour, IModComponent
{
    private const string TargetPanelName = "CreateLobbyMenu";
    private bool _isReady;
    private SliderInteractionHelper? _sliderHelper;

    private TextMeshProUGUI? _sliderLabel;
    private Slider? _teamSizeSlider;

    /// <summary>
    ///     The currently selected team size. This value is used by patches when creating a lobby
    ///     and is broadcast to other players by the NetworkedConfigManager.
    /// </summary>
    public static int SelectedTeamSize { get; private set; } = GameConstants.Game.OriginalTeamSize;

    private void Update()
    {
        if (!_isReady || _teamSizeSlider is not null) return;

        // Find the target panel only when it's active to inject the slider.
        var panelObject = GameObject.Find(TargetPanelName);
        if (panelObject is not null && panelObject.activeInHierarchy) AddSliderToPanel(panelObject.transform);
    }

    private void OnDestroy()
    {
        NetworkedConfigManager.OnTeamSizeChanged -= UpdateSliderFromNetwork;
    }

    /// <summary>
    ///     Initializes the component and subscribes to network events to keep the slider in sync.
    /// </summary>
    public void Initialize(ManualLogSource log)
    {
        _isReady = true;
        NetworkedConfigManager.OnTeamSizeChanged += UpdateSliderFromNetwork;
    }

    /// <summary>
    ///     Creates and configures a new UI slider GameObject, complete with a label and graphics,
    ///     and adds it to the target panel. It also repositions existing buttons to make room.
    /// </summary>
    private void AddSliderToPanel(Transform panelTransform)
    {
        if (panelTransform.Find("LobbySizeSlider") != null) return;
        UnlimitedMagesPlugin.Log?.LogInfo($"Injecting team size slider into '{panelTransform.name}'.");

        // Define a thematic color palette
        var bgColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
        var fillColor = new Color(0.3f, 0.65f, 1.0f, 0.9f);
        var handleColor = new Color(0.9f, 0.9f, 0.9f, 1f);

        // --- Create Main Slider Object ---
        var sliderObj = new GameObject("LobbySizeSlider");
        sliderObj.transform.SetParent(panelTransform, false);

        _teamSizeSlider = sliderObj.AddComponent<Slider>();
        _sliderHelper = sliderObj.AddComponent<SliderInteractionHelper>();

        var bgImage = sliderObj.AddComponent<Image>();
        bgImage.color = bgColor;

        var rect = sliderObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(300, 25);

        // --- Reposition Buttons ---
        var createLobbyButton = panelTransform.Find("CreateLobby");
        var backButton = panelTransform.Find("back (1)");
        if (createLobbyButton != null && backButton != null)
        {
            var createLobbyRect = createLobbyButton.GetComponent<RectTransform>();
            var backButtonRect = backButton.GetComponent<RectTransform>();
            var sliderPosition = createLobbyRect.anchoredPosition;
            var verticalShift = -80f;
            createLobbyRect.anchoredPosition += new Vector2(0, verticalShift);
            backButtonRect.anchoredPosition += new Vector2(0, verticalShift);
            rect.anchoredPosition = sliderPosition;
        }
        else
        {
            UnlimitedMagesPlugin.Log?.LogError("Could not find 'CreateLobby' or 'back (1)' buttons. Slider will be placed at a default position.");
            rect.anchoredPosition = new Vector2(0, -100);
        }

        // --- Create Slider Children ---
        
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

        // --- Create Label ---
        var textObj = new GameObject("LobbySizeLabel");
        textObj.transform.SetParent(sliderObj.transform, false);
        _sliderLabel = textObj.AddComponent<TextMeshProUGUI>();
        _sliderLabel.fontSize = 20;
        _sliderLabel.color = Color.white;
        _sliderLabel.alignment = TextAlignmentOptions.Center;
        var labelRect = textObj.GetComponent<RectTransform>();
        labelRect.anchoredPosition = new Vector2(0, 30);
        labelRect.sizeDelta = new Vector2(300, 30);

        // --- Configure Slider Logic ---
        _teamSizeSlider.direction = Slider.Direction.LeftToRight;
        _teamSizeSlider.minValue = GameConstants.Game.OriginalTeamSize;
        _teamSizeSlider.maxValue = GameConstants.Game.MaxTeamSize;
        _teamSizeSlider.wholeNumbers = true;
        _teamSizeSlider.value = SelectedTeamSize;
        UpdateSliderDisplay((int)_teamSizeSlider.value);
        _teamSizeSlider.onValueChanged.AddListener(OnSliderValueChanged);
    }

    private void OnSliderValueChanged(float value)
    {
        var intValue = (int)value;
        UpdateSliderDisplay(intValue);

        SelectedTeamSize = intValue;

        // If we are the host, broadcast this change to all clients.
        if (NetworkedConfigManager.Instance is not null)
            NetworkedConfigManager.Instance.SetAndBroadcastTeamSize(intValue);
    }

    private void UpdateSliderDisplay(int value)
    {
        if (_sliderLabel is not null)
            _sliderLabel.text = $"Team Size: {value} vs {value}";
    }

    private void UpdateSliderFromNetwork(int value)
    {
        // Do not update the slider value if the user is currently dragging it.
        if (_sliderLabel is null || _teamSizeSlider is null || (_sliderHelper is not null && _sliderHelper.IsBeingDragged)) return;

        UpdateSliderDisplay(value);
        _teamSizeSlider.SetValueWithoutNotify(value);
    }
}
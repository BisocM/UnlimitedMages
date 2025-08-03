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

namespace UnlimitedMages.UI;

internal class SliderInteractionHelper : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public bool IsBeingDragged { get; private set; }
    public void OnPointerDown(PointerEventData eventData) => IsBeingDragged = true;
    public void OnPointerUp(PointerEventData eventData) => IsBeingDragged = false;
}

internal sealed class UnlimitedMagesSlider : MonoBehaviour, IModComponent
{
    private const string TargetPanelName = "CreateLobbyMenu";
    private bool _isReady;
    private SliderInteractionHelper? _sliderHelper;
    private TextMeshProUGUI? _sliderLabel;
    private Slider? _teamSizeSlider;

    public static int SelectedTeamSize { get; private set; } = GameConstants.Game.MinimumTeamSize;

    private void Update()
    {
        if (!_isReady || _teamSizeSlider != null) return;

        var panelObject = GameObject.Find(TargetPanelName);
        if (panelObject != null && panelObject.activeInHierarchy)
            AddSliderToPanel(panelObject.transform);
    }

    private void OnDestroy()
    {
        EventBus.Unsubscribe<ConfigReadyEvent>(OnConfigReady_UpdateSlider);
    }

    public void Initialize(ManualLogSource log)
    {
        _isReady = true;

        EventBus.Subscribe<ConfigReadyEvent>(OnConfigReady_UpdateSlider);
    }

    private void AddSliderToPanel(Transform panelTransform)
    {
        if (panelTransform.Find("LobbySizeSlider") != null) return;
        UnlimitedMagesPlugin.Log?.LogInfo($"Injecting team size slider into '{panelTransform.name}'.");

        var bgColor = new Color(0.1f, 0.1f, 0.15f, 0.8f);
        var fillColor = new Color(0.3f, 0.65f, 1.0f, 0.9f);
        var handleColor = new Color(0.9f, 0.9f, 0.9f, 1f);

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
        labelRect.anchoredPosition = new Vector2(0, 30);
        labelRect.sizeDelta = new Vector2(300, 30);

        _teamSizeSlider.direction = Slider.Direction.LeftToRight;
        _teamSizeSlider.minValue = GameConstants.Game.MinimumTeamSize;
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

        // Publish an event for other systems to consume, rather than calling them directly.
        if (ConfigManager.Instance is { IsConfigReady: true })
            EventBus.Publish(new HostTeamSizeChangedEvent(intValue));
    }

    private void UpdateSliderDisplay(int value)
    {
        if (_sliderLabel != null)
            _sliderLabel.text = $"Team Size: {value} vs {value}";
    }

    private void OnConfigReady_UpdateSlider(ConfigReadyEvent evt)
    {
        if (_sliderLabel == null || _teamSizeSlider == null || (_sliderHelper != null && _sliderHelper.IsBeingDragged)) return;

        UpdateSliderDisplay(evt.TeamSize);
        _teamSizeSlider.SetValueWithoutNotify(evt.TeamSize);
    }
}
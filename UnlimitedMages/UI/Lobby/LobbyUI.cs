using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;
using Steamworks;
using UnityEngine;
using UnlimitedMages.System.Components;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.UI.Lobby;

/// <summary>
///     A MonoBehaviour that renders the custom lobby and team selection UI using Unity's immediate mode GUI (OnGUI).
///     It replaces the game's default player list panels.
/// </summary>
internal class LobbyUI : MonoBehaviour
{
    private const float RefWidth = 1920f;

    private static readonly FieldInfo? LobbyScreenField = AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.LobbyScreenField);
    private static readonly FieldInfo? InGameLobbyField = AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.InGameLobbyField);
    private GUIStyle _balanceButtonStyle = null!;
    private Texture2D _headerBgTexture = null!;
    private GUIStyle _headerStyle = null!;
    private Texture2D _hoverTexture = null!;
    private GUIStyle _kickButtonStyle = null!;

    private Rect _lobbyWindowRect;
    private ManualLogSource? _log;
    private MainMenuManager? _mainMenuManager;
    private GUIStyle _nameStyle = null!;
    private GUIStyle _notReadyButtonStyle = null!;
    private GUIStyle _placeholderStyle = null!;
    private GUIStyle _rankStyle = null!;
    private Texture2D _readyBgTexture = null!;
    private GUIStyle _readyButtonStyle = null!;
    private Vector2 _scrollPositionLobby;
    private Vector2 _scrollPositionTeam1;
    private Vector2 _scrollPositionTeam2;
    private SessionManager? _sessionManager;
    private LobbyStateManager? _stateManager;
    private Rect _teamSelectWindowRect;

    private bool _uiInitialized;
    private GUIStyle _windowStyle = null!;

    private void Start()
    {
        _log = UnlimitedMagesPlugin.Log;
        _stateManager = LobbyStateManager.Instance;
        _sessionManager = SessionManager.Instance;
        if (_stateManager == null) _log?.LogError("LobbyStateManager instance not found! The custom UI will not function.");

        _log?.LogInfo("LobbyUI rendering component has started.");
    }

    private void OnGUI()
    {
        if (_mainMenuManager == null) _mainMenuManager = FindFirstObjectByType<MainMenuManager>();
        if (_mainMenuManager == null || _stateManager == null || LobbyScreenField == null || InGameLobbyField == null) return;

        // Hide the game's original player list UI panels.
        var lobbyScreen = (GameObject)LobbyScreenField.GetValue(_mainMenuManager);
        if (lobbyScreen != null && lobbyScreen.activeInHierarchy)
        {
            var playerListParent = lobbyScreen.transform.Find("Image/Image (1)");
            if (playerListParent != null && playerListParent.gameObject.activeSelf) playerListParent.gameObject.SetActive(false);
        }

        var isTeamSelection = InGameLobbyField.GetValue(_mainMenuManager) is GameObject inGameLobby && inGameLobby.activeInHierarchy;

        // Only draw the UI if we are on the correct menu screen.
        if (!lobbyScreen.activeInHierarchy && !isTeamSelection) return;

        InitializeStyles();
        CalculateLayout();

        GUI.depth = 0; // Ensure this UI renders on top.

        // Draw the appropriate window based on whether we are in the main lobby or team selection phase.
        if (isTeamSelection)
        {
            _teamSelectWindowRect = GUI.Window(GetHashCode() + 2, _teamSelectWindowRect, DrawTeamSelectionWindow, "Team Selection", _windowStyle);
            DrawReadyButton();
            DrawBalanceButton();
        }
        else
        {
            _lobbyWindowRect = GUI.Window(GetHashCode() + 1, _lobbyWindowRect, DrawLobbyWindow, "Lobby Players", _windowStyle);
        }
    }

    /// <summary>
    ///     Draws the window that shows unassigned players.
    /// </summary>
    private void DrawLobbyWindow(int windowID)
    {
        // Show a waiting message until the client has synced with the host.
        if (_sessionManager != null && !_sessionManager.LobbyStateSynced)
        {
            GUILayout.Label("Synchronizing with host...", _placeholderStyle);
            return;
        }

        var unassignedPlayers = _stateManager!.GetUnassignedPlayers().ToList();

        _scrollPositionLobby = GUILayout.BeginScrollView(_scrollPositionLobby, false, true, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);

        if (!unassignedPlayers.Any())
            GUILayout.Label("Waiting for players...", _placeholderStyle);
        else
            foreach (var player in unassignedPlayers)
                DrawPlayerEntry(player);

        GUILayout.EndScrollView();
        GUI.DragWindow();
    }

    /// <summary>
    ///     Draws the main window for team selection, containing two columns for the teams.
    /// </summary>
    private void DrawTeamSelectionWindow(int windowID)
    {
        GUILayout.BeginHorizontal();
        DrawTeamList(0, "Sorcerers", ref _scrollPositionTeam1);
        GUILayout.Box("", new GUIStyle { normal = { background = _headerBgTexture } }, GUILayout.Width(2), GUILayout.ExpandHeight(true)); // Vertical separator
        DrawTeamList(2, "Warlocks", ref _scrollPositionTeam2);
        GUILayout.EndHorizontal();
        GUI.DragWindow();
    }

    /// <summary>
    ///     Draws a scrollable list for a single team.
    /// </summary>
    private void DrawTeamList(int teamId, string teamName, ref Vector2 scrollPos)
    {
        GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.Label(teamName, _headerStyle, GUILayout.Height(30));

        scrollPos = GUILayout.BeginScrollView(scrollPos, false, true, GUI.skin.horizontalScrollbar, GUI.skin.verticalScrollbar, GUI.skin.box);

        if (_stateManager!.Teams.TryGetValue(teamId, out var players) && players != null)
            for (var i = 0; i < players.Length; i++)
            {
                var player = players[i];
                if (player != null)
                    DrawPlayerEntry(player);
                else
                    GUILayout.Label($"<color=#777777>... Slot {i + 1} empty ...</color>", _placeholderStyle, GUILayout.Height(28));
            }
        else
            GUILayout.Label("Team not initialized.", _placeholderStyle);

        GUILayout.EndScrollView();
        GUILayout.EndVertical();
    }

    /// <summary>
    ///     Draws a single player entry row, including their rank, name, and a kick button for the host.
    /// </summary>
    private void DrawPlayerEntry(PlayerLobbyData player)
    {
        var rowRect = GUILayoutUtility.GetRect(0, 28, GUILayout.ExpandWidth(true));

        // Highlight the row if the player is ready.
        if (player.IsReady) GUI.DrawTexture(rowRect, _readyBgTexture);

        // Highlight the row on mouse hover.
        if (Event.current.type == EventType.Repaint && rowRect.Contains(Event.current.mousePosition)) GUI.DrawTexture(rowRect, _hoverTexture);

        var padding = 5f;
        var currentX = rowRect.x + padding;

        // Draw player rank with appropriate color.
        var rankWidth = 140f;
        var rankRect = new Rect(currentX, rowRect.y, rankWidth, rowRect.height);
        var rankColor = GetRankColor(player.Rank);
        var hexColor = ColorUtility.ToHtmlStringRGB(rankColor);
        GUI.Label(rankRect, $"<color=#{hexColor}>[{player.Rank.Replace("lvl", "Lvl")}]</color>", _rankStyle);
        currentX += rankWidth;

        // Draw kick button if the local player is the host and the entry is not for themselves.
        var kickButtonWidth = 54f;
        var shouldShowKickButton = _stateManager!.IsHost() && player.SteamId != SteamUser.GetSteamID().ToString() && !string.IsNullOrEmpty(player.SteamId);
        var kickButtonOffset = shouldShowKickButton ? kickButtonWidth : 0f;

        // Draw player name as a button to open their Steam profile.
        var nameWidth = rowRect.width - rankWidth - kickButtonOffset - padding * 2f;
        var nameRect = new Rect(currentX, rowRect.y, nameWidth, rowRect.height);
        var buttonContent = new GUIContent(player.FullName, $"View {player.FullName}'s Steam Profile");
        if (GUI.Button(nameRect, buttonContent, _nameStyle))
            if (!string.IsNullOrEmpty(player.SteamId))
                _stateManager!.OpenSteamProfile(player.SteamId);

        if (shouldShowKickButton)
        {
            var kickRect = new Rect(rowRect.xMax - kickButtonWidth + padding, rowRect.y, 50f, rowRect.height);
            if (GUI.Button(kickRect, "Kick", _kickButtonStyle)) _stateManager.KickPlayer(player.SteamId);
        }

        // Draw tooltip for the profile button.
        if (Event.current.type == EventType.Repaint && nameRect.Contains(Event.current.mousePosition))
        {
            var tooltip = GUI.tooltip;
            if (!string.IsNullOrEmpty(tooltip))
            {
                var tooltipStyle = new GUIStyle(GUI.skin.box) { wordWrap = true };
                var tooltipSize = tooltipStyle.CalcSize(new GUIContent(tooltip));
                GUI.Box(new Rect(Event.current.mousePosition.x + 15, Event.current.mousePosition.y + 15, tooltipSize.x, tooltipSize.y), tooltip);
            }
        }
    }

    /// <summary>
    ///     Draws the "Ready" / "Not Ready" button for the local player.
    /// </summary>
    private void DrawReadyButton()
    {
        if (_stateManager == null) return;
        var localSteamId = SteamUser.GetSteamID().ToString();
        var localPlayer = _stateManager.AllPlayers.FirstOrDefault(p => p.SteamId == localSteamId);
        if (localPlayer == null) return;

        var isReady = localPlayer.IsReady;
        var buttonRect = new Rect(_teamSelectWindowRect.x, _teamSelectWindowRect.y - 45, 130, 40);
        var buttonText = isReady ? "READY" : "NOT READY";
        var buttonStyle = isReady ? _readyButtonStyle : _notReadyButtonStyle;

        if (!GUI.Button(buttonRect, buttonText, buttonStyle)) return;

        _stateManager.SetLocalPlayerReady(!isReady);
    }

    /// <summary>
    ///     Draws the "Balance Teams" button, visible only to the host.
    /// </summary>
    private void DrawBalanceButton()
    {
        if (_stateManager == null || !_stateManager.IsHost()) return;

        var buttonWidth = 150f;
        var buttonHeight = 40f;
        var buttonRect = new Rect(_teamSelectWindowRect.x + _teamSelectWindowRect.width - buttonWidth, _teamSelectWindowRect.y - buttonHeight - 5, buttonWidth, buttonHeight);

        if (!GUI.Button(buttonRect, "BALANCE", _balanceButtonStyle)) return;

        _log?.LogInfo("Host clicked Balance Teams button.");
        _stateManager.BalanceTeams();
    }

    /// <summary>
    ///     Initializes all GUIStyle objects for the custom UI. This is only run once.
    /// </summary>
    private void InitializeStyles()
    {
        if (_uiInitialized) return;
        _log?.LogDebug("Initializing UI styles for custom lobby.");

        var bgColor = new Color(0.1f, 0.1f, 0.12f, 0.95f);
        var readyBgColor = new Color(0.1f, 0.4f, 0.15f, 0.6f);
        var nameColor = new Color(0.6f, 0.8f, 1.0f);
        var hoverBgColor = new Color(0.25f, 0.25f, 0.3f, 1.0f);
        var headerBgColor = new Color(0.15f, 0.15f, 0.18f, 1.0f);

        var defaultFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var bgTexture = CreateSolidColorTexture(bgColor);
        _readyBgTexture = CreateSolidColorTexture(readyBgColor);
        _hoverTexture = CreateSolidColorTexture(hoverBgColor);
        _headerBgTexture = CreateSolidColorTexture(headerBgColor);
        var transparentTexture = CreateSolidColorTexture(new Color(0, 0, 0, 0));

        _windowStyle = new GUIStyle(GUI.skin.window)
        {
            normal = { background = bgTexture, textColor = Color.white },
            onNormal = { background = bgTexture, textColor = Color.white },
            padding = new RectOffset(10, 10, 25, 10),
            fontSize = 16,
            font = defaultFont
        };

        _headerStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white, background = _headerBgTexture },
            padding = new RectOffset(0, 0, 5, 5),
            font = defaultFont
        };

        _nameStyle = new GUIStyle
        {
            normal = { textColor = nameColor, background = transparentTexture },
            hover = { textColor = Color.cyan, background = transparentTexture },
            active = { textColor = Color.white, background = transparentTexture },
            alignment = TextAnchor.MiddleLeft,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(5, 5, 0, 0),
            font = defaultFont
        };

        _rankStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleLeft,
            fontSize = 14,
            richText = true,
            normal = { textColor = Color.white },
            padding = new RectOffset(5, 5, 0, 0),
            font = defaultFont
        };

        _placeholderStyle = new GUIStyle(_rankStyle)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Italic
        };

        _kickButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            font = defaultFont,
            normal = { textColor = new Color(1f, 0.6f, 0.6f) },
            hover = { textColor = Color.red }
        };

        _readyButtonStyle = new GUIStyle(GUI.skin.button)
        {
            font = defaultFont,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.7f, 1f, 0.7f), background = CreateSolidColorTexture(new Color(0.2f, 0.4f, 0.2f, 0.9f)) },
            hover = { textColor = Color.white, background = CreateSolidColorTexture(new Color(0.3f, 0.6f, 0.3f, 0.9f)) },
            active = { textColor = Color.white, background = CreateSolidColorTexture(new Color(0.25f, 0.5f, 0.25f, 0.9f)) }
        };

        _notReadyButtonStyle = new GUIStyle(GUI.skin.button)
        {
            font = defaultFont,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.7f, 0.7f), background = CreateSolidColorTexture(new Color(0.4f, 0.2f, 0.2f, 0.9f)) },
            hover = { textColor = Color.white, background = CreateSolidColorTexture(new Color(0.6f, 0.3f, 0.3f, 0.9f)) },
            active = { textColor = Color.white, background = CreateSolidColorTexture(new Color(0.5f, 0.25f, 0.25f, 0.9f)) }
        };

        _balanceButtonStyle = new GUIStyle(GUI.skin.button)
        {
            font = defaultFont,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(1f, 0.9f, 0.6f), background = CreateSolidColorTexture(new Color(0.4f, 0.35f, 0.1f, 0.9f)) },
            hover = { textColor = Color.white, background = CreateSolidColorTexture(new Color(0.6f, 0.5f, 0.2f, 0.9f)) },
            active = { textColor = Color.white, background = CreateSolidColorTexture(new Color(0.5f, 0.4f, 0.15f, 0.9f)) }
        };

        GUI.skin.verticalScrollbar.fixedWidth = 10;
        GUI.skin.verticalScrollbarThumb.fixedWidth = 10;

        _uiInitialized = true;
        _log?.LogInfo("Custom lobby UI styles initialized with robust settings.");
    }

    /// <summary>
    ///     Calculates the screen positions and sizes of the UI windows based on screen resolution.
    /// </summary>
    private void CalculateLayout()
    {
        var scale = Screen.width / RefWidth;

        var lobbyX = 1410f * scale;
        var lobbyY = 730f * scale;
        var lobbyWidth = 490f * scale;
        var lobbyHeight = 240f * scale;
        _lobbyWindowRect = new Rect(lobbyX, lobbyY, lobbyWidth, lobbyHeight);

        var teamX = 550f * scale;
        var teamY = 580f * scale;
        var teamWidth = 800f * scale;
        var teamHeight = 280f * scale;
        _teamSelectWindowRect = new Rect(teamX, teamY, teamWidth, teamHeight);
    }

    private static Texture2D CreateSolidColorTexture(Color color)
    {
        var texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
        texture.Apply();
        return texture;
    }

    /// <summary>
    ///     Gets the appropriate color for a player's rank by reading the color values from the MainMenuManager instance.
    /// </summary>
    private Color GetRankColor(string rank)
    {
        if (_mainMenuManager == null) return Color.white;
        rank = rank.ToLower();
        if (rank.Contains("lackey")) return _mainMenuManager.Rank1;
        if (rank.Contains("sputterer")) return _mainMenuManager.Rank2;
        if (rank.Contains("novice")) return _mainMenuManager.Rank3;
        if (rank.Contains("apprentice")) return _mainMenuManager.Rank4;
        if (rank.Contains("savant")) return _mainMenuManager.Rank5;
        if (rank.Contains("master") && !rank.Contains("grand")) return _mainMenuManager.Rank6;
        if (rank.Contains("grand")) return _mainMenuManager.Rank7;
        if (rank.Contains("supreme")) return _mainMenuManager.Rank10;
        if (rank.Contains("archmagus")) return _mainMenuManager.Rank8;
        if (rank.Contains("prime")) return _mainMenuManager.Rank9;
        return Color.white;
    }
}
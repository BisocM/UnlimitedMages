using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using Dissonance;
using Dissonance.Integrations.FishNet;
using FishNet.Managing;
using HarmonyLib;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnlimitedMages.System.Components;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using UnlimitedMages.UI.Lobby;
using UnlimitedMages.Utilities;
using Object = UnityEngine.Object;

namespace UnlimitedMages.Patches;

/// <summary>
///     Contains Harmony patches for the <see cref="BootstrapManager" /> class.
///     These patches handle lobby creation, matchmaking, scene transitions, and versioning to support larger team sizes.
/// </summary>
[HarmonyPatch(typeof(BootstrapManager))]
internal static class BootstrapManagerPatches
{
    private static bool _eventFired;

    /// <summary>
    ///     Postfixes the lobby creation callback to tag the lobby with mod-specific data.
    ///     This ensures that only players with the same mod version can see and join the lobby.
    /// </summary>
    [HarmonyPatch(typeof(BootstrapManager), GameConstants.BootstrapManager.OnLobbyCreatedMethod)]
    [HarmonyPostfix]
    public static void OnLobbyCreated_Postfix(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK) return;

        // Reset all mod systems to a clean state for the new lobby session.
        UnlimitedMagesPlugin.Log?.LogInfo("Host created a new lobby. Resetting mod session states.");
        SessionManager.Instance?.Reset();
        ConfigManager.Instance?.Reset();
        LobbyStateManager.Instance?.Reset();

        var lobbyId = new CSteamID(callback.m_ulSteamIDLobby);

        // Tag the lobby with a modified version string and mod-specific GUID.
        SteamMatchmaking.SetLobbyData(lobbyId, "Version", GameConstants.BootstrapManager.GetModdedVersionString());
        SteamMatchmaking.SetLobbyData(
            lobbyId,
            UnlimitedMagesPlugin.ModGuid,
            UnlimitedMagesPlugin.ModVersion
        );

        UnlimitedMagesPlugin.Log?.LogInfo($"Tagged lobby {callback.m_ulSteamIDLobby} as modded with version {UnlimitedMagesPlugin.ModVersion}.");
    }

    /// <summary>
    ///     Prefixes the lobby list request to add a filter for the mod's GUID and version.
    ///     This ensures that the lobby browser only shows compatible modded games.
    /// </summary>
    [HarmonyPatch(typeof(BootstrapManager), nameof(BootstrapManager.GetLobbiesList))]
    [HarmonyPrefix]
    public static void GetLobbiesList_Prefix()
    {
        SteamMatchmaking.AddRequestLobbyListStringFilter(
            UnlimitedMagesPlugin.ModGuid,
            UnlimitedMagesPlugin.ModVersion,
            ELobbyComparison.k_ELobbyComparisonEqual
        );
    }

    /// <summary>
    ///     Transpiles the GetLobbiesList method to replace the standard application version check
    ///     with a check for the modded version string.
    /// </summary>
    [HarmonyPatch(typeof(BootstrapManager), nameof(BootstrapManager.GetLobbiesList))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> GetLobbiesList_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);

        var originalMethod = AccessTools.PropertyGetter(typeof(Application), nameof(Application.version));
        var replacementMethod = AccessTools.Method(typeof(GameConstants.BootstrapManager), nameof(GameConstants.BootstrapManager.GetModdedVersionString));

        for (var i = 0; i < newInstructions.Count; i++)
        {
            if (!newInstructions[i].Calls(originalMethod)) continue;
            UnlimitedMagesPlugin.Log?.LogInfo("Patching GetLobbiesList to search for modded version string...");
            newInstructions[i] = new CodeInstruction(OpCodes.Call, replacementMethod);
            UnlimitedMagesPlugin.Log?.LogInfo("Successfully patched GetLobbiesList.");
            break; // Stop after the first replacement
        }

        return newInstructions;
    }

    /// <summary>
    ///     Postfixes the Awake method to publish a global event indicating that the BootstrapManager is ready.
    ///     This is used to trigger the injection of the mod's components.
    /// </summary>
    [HarmonyPatch(GameConstants.BootstrapManager.AwakeMethod)]
    [HarmonyPostfix]
    public static void Awake_Postfix()
    {
        if (_eventFired) return;
        EventBus.Publish(new BootstrapReadyEvent());
        _eventFired = true;
    }

    /// <summary>
    ///     Transpiles the CreateLobby method to use the dynamically selected team size from the UI slider
    ///     instead of the hardcoded value.
    /// </summary>
    [HarmonyPatch(nameof(BootstrapManager.CreateLobby), typeof(bool))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> CreateLobby_Transpiler(IEnumerable<CodeInstruction> instructions) =>
        TranspileGetTeamSize(instructions, true);

    /// <summary>
    ///     Transpiles the lobby list update method to allow lobbies of any size to be displayed,
    ///     bypassing the default check against a hardcoded max player count.
    /// </summary>
    [HarmonyPatch(GameConstants.BootstrapManager.OnGetLobbyListMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnGetLobbyList_Transpiler(IEnumerable<CodeInstruction> instructions) =>
        TranspileAllowAnyTeamSize(instructions);

    /// <summary>
    ///     Postfixes the lobby entry callback to reset mod state for clients joining a lobby.
    /// </summary>
    [HarmonyPatch(GameConstants.BootstrapManager.OnLobbyEnteredMethod)]
    [HarmonyPostfix]
    public static void OnLobbyEntered_Postfix(LobbyEnter_t callback)
    {
        var isHost = SteamMatchmaking.GetLobbyOwner(new CSteamID(callback.m_ulSteamIDLobby)) == SteamUser.GetSteamID();
        if (isHost) return;

        UnlimitedMagesPlugin.Log?.LogInfo("Client has entered a lobby. Resetting mod session state.");
        SessionManager.Instance?.Reset();
        ConfigManager.Instance?.Reset();
        LobbyStateManager.Instance?.Reset();

        var lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        var memberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);

        if (memberLimit <= GameConstants.Game.OriginalTeamSize * GameConstants.Game.NumTeams) return;

        UnlimitedMagesPlugin.Log?.LogInfo($"Client entered lobby. Correcting max players to Steam value: {memberLimit}");
    }

    /// <summary>
    ///     Prefixes the scene change method to inject a custom cleanup routine.
    ///     This ensures networking components like Dissonance and FishNet are properly shut down before returning to the main menu.
    /// </summary>
    [HarmonyPatch(GameConstants.BootstrapManager.ChangeSceneAfterCleanupMethod, typeof(string))]
    [HarmonyPrefix]
    public static bool ChangeSceneAfterCleanup_Prefix(string sceneName, ref IEnumerator __result)
    {
        __result = CustomChangeSceneAfterCleanup(sceneName);
        return false; // Prevents the original method from running.
    }

    /// <summary>
    ///     Transpiles the lobby data retrieval method to use the modded version string for validation
    ///     and to dynamically check the lobby member limit from Steam instead of using a hardcoded value.
    /// </summary>
    [HarmonyPatch(typeof(BootstrapManager), GameConstants.BootstrapManager.OnGetLobbyData)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnGetLobbyData_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);

        var getVersionOriginal = AccessTools.PropertyGetter(typeof(Application), nameof(Application.version));
        var getVersionReplacement = AccessTools.Method(typeof(GameConstants.BootstrapManager), nameof(GameConstants.BootstrapManager.GetModdedVersionString));

        // Patch version check.
        for (var i = 0; i < newInstructions.Count; i++)
        {
            if (!newInstructions[i].Calls(getVersionOriginal)) continue;
            UnlimitedMagesPlugin.Log?.LogInfo("Patching OnGetLobbyData to use modded version string...");
            newInstructions[i] = new CodeInstruction(OpCodes.Call, getVersionReplacement);
            UnlimitedMagesPlugin.Log?.LogInfo("Successfully patched OnGetLobbyData version check.");
            break; // Stop after the first replacement, which is the one used in the check
        }

        var getNumLobbyMembers = AccessTools.Method(typeof(SteamMatchmaking), nameof(SteamMatchmaking.GetNumLobbyMembers));
        var getLobbyMemberLimit = AccessTools.Method(typeof(SteamMatchmaking), nameof(SteamMatchmaking.GetLobbyMemberLimit));

        // Patch lobby member limit check.
        for (var i = 0; i < newInstructions.Count; i++)
        {
            // Find the pattern: `GetNumLobbyMembers()` call followed by `ldc.i4.8` (loading the number 8).
            if (!newInstructions[i].Calls(getNumLobbyMembers) || i + 1 >= newInstructions.Count || newInstructions[i + 1].opcode != OpCodes.Ldc_I4_8) continue;
            UnlimitedMagesPlugin.Log?.LogInfo("Patching OnGetLobbyData to use dynamic lobby limit...");

            // The original code compares the number of members to 8. We replace the '8' with a call to GetLobbyMemberLimit.
            var loadIdInstruction = newInstructions[i - 2]; // ldsfld CurrentLobbyID
            var newIdInstruction = newInstructions[i - 1]; // newobj CSteamID

            newInstructions[i + 1] = new CodeInstruction(OpCodes.Call, getLobbyMemberLimit);

            // We need to pass the lobby ID to GetLobbyMemberLimit, so we re-insert the instructions that load it.
            newInstructions.Insert(i + 1, newIdInstruction.Clone());
            newInstructions.Insert(i + 1, loadIdInstruction.Clone());

            UnlimitedMagesPlugin.Log?.LogInfo("Successfully patched OnGetLobbyData.");
            break; // We've made the change, so the search stops.
        }

        return newInstructions;
    }

    /// <summary>
    ///     A custom coroutine that safely tears down networking and voice communication systems before changing scenes.
    ///     This prevents lingering connections and errors when returning to the main menu.
    /// </summary>
    private static IEnumerator CustomChangeSceneAfterCleanup(string sceneName)
    {
        // Stop Dissonance voice chat services.
        var dissonanceFishNet = Object.FindFirstObjectByType<DissonanceFishNetComms>();
        if (dissonanceFishNet != null)
        {
            var stopitMethod = AccessTools.Method(typeof(DissonanceFishNetComms), GameConstants.Dissonance.StopItMethod);
            if (stopitMethod != null)
            {
                UnlimitedMagesPlugin.Log?.LogInfo("Calling stopit() on DissonanceFishNetComms.");
                stopitMethod.Invoke(dissonanceFishNet, null);
            }
            else
            {
                UnlimitedMagesPlugin.Log?.LogWarning("Could not find 'stopit' method on DissonanceFishNetComms. VoIP cleanup might be incomplete.");
            }
        }

        yield return null;

        var dissonanceComms = Object.FindFirstObjectByType<DissonanceComms>();
        if (dissonanceComms != null)
        {
            UnlimitedMagesPlugin.Log?.LogInfo("Destroying DissonanceComms GameObject.");
            Object.Destroy(dissonanceComms.gameObject);
        }

        // Stop FishNet networking services.
        var networkManager = Object.FindFirstObjectByType<NetworkManager>();
        var fishySteamworks = Object.FindFirstObjectByType<FishySteamworks.FishySteamworks>();

        if (BootstrapManager.CurrentLobbyID != 0uL)
        {
            SteamMatchmaking.LeaveLobby(new CSteamID(BootstrapManager.CurrentLobbyID));
            BootstrapManager.CurrentLobbyID = 0uL;
        }

        if (networkManager != null)
        {
            if (networkManager.ServerManager.Started) networkManager.ServerManager.StopConnection(true);
            if (networkManager.ClientManager.Started) networkManager.ClientManager.StopConnection();
        }

        if (fishySteamworks != null)
        {
            fishySteamworks.StopConnection(false);
            fishySteamworks.StopConnection(true);
        }

        yield return new WaitForSeconds(0.5f);

        // Final cleanup and scene load.
        GameObject[] playbackPrefabs = GameObject.FindGameObjectsWithTag("playbackprefab");
        foreach (var prefab in playbackPrefabs) Object.Destroy(prefab);

        yield return null;

        SceneManager.LoadScene(sceneName);

        if (BootstrapManager.instance == null) yield break;

        BootstrapManager.instance.GoToMenu();
        AccessTools.Field(typeof(BootstrapManager), GameConstants.BootstrapManager.HasLeaveGameFinishedField).SetValue(BootstrapManager.instance, true);
    }

    /// <summary>
    ///     A transpiler helper that modifies IL instructions to allow lobbies of any size to be visible in the server list.
    ///     It achieves this by duplicating the result of `GetLobbyMemberLimit`, making the comparison `limit <= limit`, which is always true.
    /// </summary>
    private static IEnumerable<CodeInstruction> TranspileAllowAnyTeamSize(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var getLobbyMemberLimit = AccessTools.Method(typeof(SteamMatchmaking), nameof(SteamMatchmaking.GetLobbyMemberLimit));

        for (var i = 0; i < newInstructions.Count; i++)
        {
            // Find the pattern: `GetLobbyMemberLimit()` call followed by `ldc.i4.8` (loading the number 8).
            if (!newInstructions[i].Calls(getLobbyMemberLimit) || i + 1 >= newInstructions.Count || newInstructions[i + 1].opcode != OpCodes.Ldc_I4_8) continue;
            UnlimitedMagesPlugin.Log?.LogInfo("Patching OnGetLobbyList to allow visibility of all lobby sizes...");

            // Replaces `ldc.i4.8` with `dup`, which duplicates the value on top of the stack (the member limit).
            // This effectively changes the check from `current_players <= 8` to `current_players <= member_limit`.
            newInstructions[i + 1] = new CodeInstruction(OpCodes.Dup);
            UnlimitedMagesPlugin.Log?.LogInfo("Successfully patched OnGetLobbyList.");
            break;
        }

        return newInstructions;
    }

    /// <summary>
    ///     Prefixes the lobby chat update callback to detect when a player leaves the lobby.
    ///     When a leave event occurs, the player is removed from the custom lobby state manager.
    /// </summary>
    [HarmonyPatch(GameConstants.BootstrapManager.OnLobbyChatUpdateMethod)]
    [HarmonyPrefix]
    public static void OnLobbyChatUpdate_Prefix(LobbyChatUpdate_t callback)
    {
        var stateChange = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;

        // Check if the update is a leave, disconnect, kick, or ban event.
        var isLeaveEvent = (stateChange & (EChatMemberStateChange.k_EChatMemberStateChangeLeft |
                                           EChatMemberStateChange.k_EChatMemberStateChangeDisconnected |
                                           EChatMemberStateChange.k_EChatMemberStateChangeKicked |
                                           EChatMemberStateChange.k_EChatMemberStateChangeBanned)) != 0;

        if (!isLeaveEvent || LobbyStateManager.Instance == null) return;

        // Remove the player from our internal tracking.
        var steamIdUserChanged = new CSteamID(callback.m_ulSteamIDUserChanged);
        LobbyStateManager.Instance.RemovePlayerBySteamId(steamIdUserChanged.ToString());
    }

    /// <summary>
    ///     A generic transpiler helper to replace a hardcoded team size (4 or 8) with a call to get the configured team size.
    /// </summary>
    /// <param name="instructions">The original IL instructions.</param>
    /// <param name="multiplyByTeams">If true, multiplies the team size by the number of teams to get the total lobby size.</param>
    private static IEnumerable<CodeInstruction> TranspileGetTeamSize(IEnumerable<CodeInstruction> instructions, bool multiplyByTeams)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var targetOpcode = multiplyByTeams ? OpCodes.Ldc_I4_8 : OpCodes.Ldc_I4_4;

        var getSelectedTeamSize = AccessTools.PropertyGetter(typeof(UnlimitedMagesSlider), nameof(UnlimitedMagesSlider.SelectedTeamSize));

        for (var i = 0; i < newInstructions.Count; i++)
        {
            if (newInstructions[i].opcode != targetOpcode) continue;

            // Replace the hardcoded integer load with a call to our dynamic property.
            newInstructions[i] = new CodeInstruction(OpCodes.Call, getSelectedTeamSize);

            // If required, also insert instructions to multiply by the number of teams.
            if (!multiplyByTeams) continue;

            newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, GameConstants.Game.NumTeams));
            newInstructions.Insert(i + 2, new CodeInstruction(OpCodes.Mul));
        }

        return newInstructions;
    }
}
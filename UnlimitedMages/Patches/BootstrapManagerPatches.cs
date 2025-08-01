using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using FishNet.Managing;
using HarmonyLib;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using UnlimitedMages.UI;
using UnlimitedMages.Utilities;
using Object = UnityEngine.Object;

namespace UnlimitedMages.Patches;

[HarmonyPatch(typeof(BootstrapManager))]
public static class BootstrapManagerPatches
{
    #region Fields

    private static bool _eventFired;

    #endregion

    #region Patches

    [HarmonyPatch("Awake")]
    [HarmonyPostfix]
    public static void Awake_Postfix()
    {
        if (_eventFired) return;
        EventBus.Publish(new BootstrapReadyEvent());
        _eventFired = true;
    }

    [HarmonyPatch(nameof(BootstrapManager.CreateLobby), typeof(bool))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> CreateLobby_Transpiler(IEnumerable<CodeInstruction> instructions) =>
        TranspileGetTeamSize(instructions, true);

    [HarmonyPatch(GameConstants.BootstrapManager.OnGetLobbyListMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnGetLobbyList_Transpiler(IEnumerable<CodeInstruction> instructions) =>
        TranspileGetTeamSize(instructions, true);

    /// <summary>
    /// Postfix patch to use the authoritative lobby size from Steamworks. This prevents issues on the client
    /// where game logic could run with an incorrect default size before the mod's configuration is synced.
    /// </summary>
    [HarmonyPatch(GameConstants.BootstrapManager.OnLobbyEnteredMethod)]
    [HarmonyPostfix]
    public static void OnLobbyEntered_Postfix(LobbyEnter_t callback)
    {
        var isHost = SteamMatchmaking.GetLobbyOwner(new CSteamID(callback.m_ulSteamIDLobby)) == SteamUser.GetSteamID();
        if (isHost) return;

        var lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        var memberLimit = SteamMatchmaking.GetLobbyMemberLimit(lobbyId);

        if (memberLimit <= GameConstants.Game.OriginalTeamSize * GameConstants.Game.NumTeams) return;

        UnlimitedMagesPlugin.Log?.LogInfo($"Client entered lobby. Correcting max players to Steam value: {memberLimit}");

        var mainMenuManager = Object.FindFirstObjectByType<MainMenuManager>();
        if (mainMenuManager != null)
        {
            AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.MaxPlayersField).SetValue(mainMenuManager, memberLimit);
        }
    }

    [HarmonyPatch(GameConstants.BootstrapManager.ChangeSceneAfterCleanupMethod, typeof(string))]
    [HarmonyPrefix]
    public static bool ChangeSceneAfterCleanup_Prefix(string sceneName, ref IEnumerator __result)
    {
        __result = CustomChangeSceneAfterCleanup(sceneName);
        return false;
    }

    [HarmonyPatch(typeof(BootstrapManager), GameConstants.BootstrapManager.OnGetLobbyData)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnGetLobbyData_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);

        var getNumLobbyMembers = AccessTools.Method(typeof(SteamMatchmaking), nameof(SteamMatchmaking.GetNumLobbyMembers));
        var getLobbyMemberLimit = AccessTools.Method(typeof(SteamMatchmaking), nameof(SteamMatchmaking.GetLobbyMemberLimit));

        for (var i = 0; i < newInstructions.Count; i++)
        {
            // Look for the call to GetNumLobbyMembers followed by loading the hardcoded value 8
            if (!newInstructions[i].Calls(getNumLobbyMembers) || i + 1 >= newInstructions.Count || newInstructions[i + 1].opcode != OpCodes.Ldc_I4_8) continue;
            UnlimitedMagesPlugin.Log?.LogInfo("Patching OnGetLobbyData to use dynamic lobby limit...");

            // The 'CSteamID' object is constructed and placed on the stack right before the GetNumLobbyMembers call.
            // That call consumes it. Just need to do the same thing for the GetLobbyMemberLimit call.
            // The instructions to create the CSteamID are at i-2 and i-1 in the original code.
            var loadIdInstruction = newInstructions[i - 2]; // ldsfld CurrentLobbyID
            var newIdInstruction = newInstructions[i - 1]; // newobj CSteamID

            // Replace 'ldc.i4.8' with the new set of instructions to get the real limit.
            newInstructions[i + 1] = new CodeInstruction(OpCodes.Call, getLobbyMemberLimit);

            // Insert the instructions to create the CSteamID argument for the new call.
            newInstructions.Insert(i + 1, newIdInstruction.Clone());
            newInstructions.Insert(i + 1, loadIdInstruction.Clone());

            UnlimitedMagesPlugin.Log?.LogInfo("Successfully patched OnGetLobbyData.");
            break; // We've made the change, so the search stops.
        }

        //  Patched code would be:
        // IL_xxxx: ldsfld       unsigned int64 BootstrapManager::CurrentLobbyID
        // IL_xxxx: newobj       instance void [com.rlabrecque.steamworks.net]Steamworks.CSteamID::.ctor(unsigned int64)
        // IL_xxxx: call         int32 [com.rlabrecque.steamworks.net]SteamMatchmaking::GetLobbyMemberLimit(...)
        return newInstructions;
    }

    #endregion

    #region Helpers

    private static IEnumerator CustomChangeSceneAfterCleanup(string sceneName)
    {
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

        GameObject[] playbackPrefabs = GameObject.FindGameObjectsWithTag("playbackprefab");
        foreach (var prefab in playbackPrefabs) Object.Destroy(prefab);

        yield return null;

        SceneManager.LoadScene(sceneName);

        if (BootstrapManager.instance == null) yield break;

        BootstrapManager.instance.GoToMenu();
        AccessTools.Field(typeof(BootstrapManager), GameConstants.BootstrapManager.HasLeaveGameFinishedField).SetValue(BootstrapManager.instance, true);
    }

    private static IEnumerable<CodeInstruction> TranspileGetTeamSize(IEnumerable<CodeInstruction> instructions, bool multiplyByTeams)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var targetOpcode = multiplyByTeams ? OpCodes.Ldc_I4_8 : OpCodes.Ldc_I4_4;

        var getSelectedTeamSize = AccessTools.PropertyGetter(typeof(UISliderInjector), nameof(UISliderInjector.SelectedTeamSize));

        for (var i = 0; i < newInstructions.Count; i++)
        {
            if (newInstructions[i].opcode != targetOpcode) continue;

            newInstructions[i] = new CodeInstruction(OpCodes.Call, getSelectedTeamSize);

            if (!multiplyByTeams) continue;

            newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, GameConstants.Game.NumTeams));
            newInstructions.Insert(i + 2, new CodeInstruction(OpCodes.Mul));
        }

        return newInstructions;
    }

    #endregion
}
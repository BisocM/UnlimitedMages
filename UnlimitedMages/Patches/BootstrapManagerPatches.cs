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
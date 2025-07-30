using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using FishNet.Managing;
using HarmonyLib;
using Steamworks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnlimitedMages.System;
using UnlimitedMages.UI;
using UnlimitedMages.Utilities;
using Object = UnityEngine.Object;

namespace UnlimitedMages.Patches;

/// <summary>
///     Contains Harmony patches for the <see cref="BootstrapManager" /> class.
/// </summary>
[HarmonyPatch(typeof(BootstrapManager))]
public static class BootstrapManagerPatches
{
    /// <summary>
    ///     This transpiler helper now gets the NetworkedConfigManager via its own static singleton instance.
    ///     It works for both static and instance methods of the patched class.
    /// </summary>
    private static IEnumerable<CodeInstruction> TranspileGetTeamSize(IEnumerable<CodeInstruction> instructions, bool multiplyByTeams)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var targetOpcode = multiplyByTeams ? OpCodes.Ldc_I4_8 : OpCodes.Ldc_I4_4;

        var getSelectedTeamSize = AccessTools.PropertyGetter(typeof(UISliderInjector), nameof(UISliderInjector.SelectedTeamSize));

        for (var i = 0; i < newInstructions.Count; i++)
        {
            if (newInstructions[i].opcode != targetOpcode) continue;

            // Replace the hardcoded number with a call to the static property.
            newInstructions[i] = new CodeInstruction(OpCodes.Call, getSelectedTeamSize);

            if (!multiplyByTeams) continue;
            
            newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Ldc_I4, GameConstants.Game.NumTeams));
            newInstructions.Insert(i + 2, new CodeInstruction(OpCodes.Mul));
        }

        return newInstructions;
    }

    [HarmonyPatch(nameof(BootstrapManager.CreateLobby), typeof(bool))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> CreateLobby_Transpiler(IEnumerable<CodeInstruction> instructions) =>
        TranspileGetTeamSize(instructions, true);

    [HarmonyPatch(GameConstants.BootstrapManager.OnGetLobbyListMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnGetLobbyList_Transpiler(IEnumerable<CodeInstruction> instructions) =>
        TranspileGetTeamSize(instructions, true);

    [HarmonyPatch(GameConstants.BootstrapManager.OnLobbyEnteredMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> OnLobbyEntered_Transpiler(IEnumerable<CodeInstruction> instructions) =>
        TranspileGetTeamSize(instructions, true);

    /// <summary>
    ///     Prefixes the original scene change coroutine to run our custom cleanup logic instead.
    ///     This ensures all network connections (FishNet, Steamworks) are properly terminated.
    /// </summary>
    /// <returns>Returns false to skip the original method entirely.</returns>
    [HarmonyPatch(GameConstants.BootstrapManager.ChangeSceneAfterCleanupMethod, typeof(string))]
    [HarmonyPrefix]
    public static bool ChangeSceneAfterCleanup_Prefix(string sceneName, ref IEnumerator __result)
    {
        __result = CustomChangeSceneAfterCleanup(sceneName);
        return false;
    }

    /// <summary>
    ///     A custom implementation of the game's scene cleanup logic. It correctly finds and
    ///     shuts down networking managers before loading the main menu scene.
    /// </summary>
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
        AccessTools.Field(typeof(BootstrapManager), "hasLeaveGameFinished").SetValue(BootstrapManager.instance, true);
    }
}

/// <summary>
///     This patch targets the Awake method of BootstrapManager to fire a one-time event,
///     signaling that the game's core systems are ready for mod components to be injected.
/// </summary>
[HarmonyPatch(typeof(BootstrapManager), "Awake")]
public static class BootstrapManager_InjectionPatch
{
    private static bool _eventFired;

    /// <summary>
    ///     After the BootstrapManager's Awake method runs, this invokes the OnBootstrapReady event.
    ///     A flag ensures this only happens once per game launch.
    /// </summary>
    [HarmonyPostfix]
    public static void Postfix()
    {
        if (_eventFired) return;
        ModLifecycleEvents.InvokeBootstrapReady();
        _eventFired = true;
    }
}
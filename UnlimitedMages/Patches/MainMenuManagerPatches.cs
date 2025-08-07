using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnlimitedMages.System.Components;
using UnlimitedMages.UI;
using UnlimitedMages.UI.Popup;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.Patches;

/// <summary>
///     Contains Harmony patches for the <see cref="MainMenuManager" /> class.
///     These patches handle lobby UI, player synchronization, map selection logic, and game start validation.
/// </summary>
[HarmonyPatch(typeof(MainMenuManager))]
internal static class MainMenuManagerPatches
{
    /// <summary>
    ///     Postfixes the Start method to initialize connections between the mod's UI/State managers and the MainMenuManager instance.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(GameConstants.MainMenuManager.StartMethod)]
    public static void Start_Postfix(MainMenuManager __instance)
    {
        if (LobbyStateManager.Instance != null) LobbyStateManager.Instance.SetMainMenuManager(__instance);

        if (ModUIManager.Instance?.LobbyUiHost != null) ModUIManager.Instance.LobbyUiHost.SetActive(true);
    }

    /// <summary>
    ///     Transpiles map selection methods (SmallMap, LargeMap) to replace the hardcoded player count
    ///     check with a dynamic value based on the configured team size.
    /// </summary>
    [HarmonyPatch(nameof(MainMenuManager.SmallMap))]
    [HarmonyPatch(nameof(MainMenuManager.LargeMap))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> MapSelect_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);

        var getInstance = AccessTools.PropertyGetter(typeof(ConfigManager), nameof(ConfigManager.Instance));
        var getTeamSize = AccessTools.PropertyGetter(typeof(ConfigManager), nameof(ConfigManager.TeamSize));

        for (var i = 0; i < newInstructions.Count; i++)
        {
            // Find the hardcoded lobby size (8) and replace it.
            if (newInstructions[i].opcode != OpCodes.Ldc_I4_8) continue;

            // Replace with instructions to get TeamSize * NumTeams.
            newInstructions[i] = new CodeInstruction(OpCodes.Call, getInstance);
            newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, getTeamSize));
            newInstructions.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_I4, GameConstants.Game.NumTeams));
            newInstructions.Insert(i + 3, new CodeInstruction(OpCodes.Mul));
        }

        return newInstructions;
    }

    /// <summary>
    ///     Prefixes the SyncHats method, which is used by the game to broadcast player join information.
    ///     The original method is prevented, and the data is instead routed to the custom <see cref="LobbyStateManager" />.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(GameConstants.MainMenuManager.SyncHatsMethod)]
    public static bool SyncHats_Prefix(string PlayerName, string PlayerRank, string steamid)
    {
        LobbyStateManager.Instance?.AddPlayer(PlayerName, PlayerRank, steamid);
        return false; // Prevent original method execution.
    }

    /// <summary>
    ///     Transpiles the SyncHats method to fix an issue where the player name was not properly clamped
    ///     before being used as a dictionary key, which is handled by the original, now-bypassed method.
    ///     This patch is largely for compatibility, as the prefix already handles the core logic.
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(GameConstants.MainMenuManager.SyncHatsMethod)]
    public static IEnumerable<CodeInstruction> SyncHats_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var addToDictMethod = AccessTools.Method(typeof(KickPlayersHolder), GameConstants.KickPlayersHolder.AddToDictMethod);
        var clampStringMethod = AccessTools.Method(typeof(MainMenuManager), GameConstants.MainMenuManager.ClampStringMethod);

        for (var i = 0; i < newInstructions.Count; i++)
        {
            if (!newInstructions[i].Calls(addToDictMethod)) continue;

            var instructionsToInsert = new List<CodeInstruction>
            {
                new(OpCodes.Ldarg_0), // `this` (MainMenuManager instance)
                new(OpCodes.Ldarg_1), // `PlayerName` argument
                new(OpCodes.Ldc_I4, GameConstants.MainMenuManager.PlayerNameClampLength),
                new(OpCodes.Call, clampStringMethod)
            };

            // Replace the original 'ldarg.1' with the block that calls ClampString.
            newInstructions.RemoveAt(i - 2);
            newInstructions.InsertRange(i - 2, instructionsToInsert);
            break;
        }

        return newInstructions;
    }

    /// <summary>
    ///     Postfixes the RemoveHat method, which is used by the game when a player leaves.
    ///     This ensures the player is also removed from the custom <see cref="LobbyStateManager" />.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(GameConstants.MainMenuManager.RemoveHatMethod)]
    public static void RemoveHat_Postfix(MainMenuManager __instance, string PlayerName)
    {
        var clampStringMethod = AccessTools.Method(typeof(MainMenuManager), GameConstants.MainMenuManager.ClampStringMethod);
        var clampedName = (string)clampStringMethod.Invoke(__instance, [PlayerName, GameConstants.MainMenuManager.PlayerNameClampLength]);

        LobbyStateManager.Instance?.RemovePlayer(clampedName);

        // Also clean up the game's internal kick dictionary to prevent issues.
        var kickDictionaryField = AccessTools.Field(typeof(KickPlayersHolder), GameConstants.KickPlayersHolder.NameToSteamIdField);
        var kickDictionary = (Dictionary<string, string>)kickDictionaryField.GetValue(__instance.kickplayershold);

        if (kickDictionary != null && kickDictionary.ContainsKey(clampedName)) kickDictionary.Remove(clampedName);
    }
    
    [HarmonyPrefix]
    [HarmonyPatch(nameof(MainMenuManager.ChangeTeamText))]
    public static bool ChangeTeamText_Prefix() => false;

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MainMenuManager.DestroySpinningGuy))]
    public static bool DestroySpinningGuy_Prefix() => false;

    /// <summary>
    ///     Prefixes the game start method to add a "ready check".
    ///     If the host tries to start while players are not marked as ready, a confirmation popup is shown.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(nameof(MainMenuManager.ActuallyStartGame))]
    public static bool ActuallyStartGame_Prefix(MainMenuManager __instance)
    {
        var stateManager = LobbyStateManager.Instance;
        var uiManager = ModUIManager.Instance;

        // If not the host or managers are missing, allow the original method to run.
        if (stateManager == null || uiManager == null || !stateManager.IsHost()) return true;

        // If all players are ready, allow the game to start.
        if (stateManager.AreAllPlayersReady())
        {
            UnlimitedMagesPlugin.Log?.LogInfo("All players are ready. Starting the game.");
            return true;
        }

        UnlimitedMagesPlugin.Log?.LogWarning("Host attempted to start the game, but not all players are ready. Displaying confirmation popup.");

        // Configure and show a popup asking the host to confirm.
        var title = "Players Not Ready";
        var message = "One or more players have not marked themselves as ready.\n\n" +
                      "Do you want to force the game to start anyway?";

        var buttons = new[]
        {
            new PopupButtonData(PopupButton.Ok, "START"),
            new PopupButtonData(PopupButton.Warning, "WAIT")
        };

        uiManager.ShowPopup(title, message, OnButtonClicked, buttons);

        return false; // Prevent the original method from running.

        void OnButtonClicked(PopupButton buttonType)
        {
            if (buttonType == PopupButton.Ok) // Corresponds to the "START" button.
            {
                UnlimitedMagesPlugin.Log?.LogInfo("Host chose to force start the game.");
                __instance.mmmn.ActuallyStartGame(); // Manually call the networked start method.
            }
            else // Corresponds to the "WAIT" button.
            {
                UnlimitedMagesPlugin.Log?.LogInfo("Host chose to wait for players to ready up.");
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;
using UnlimitedMages.Networking;
using UnlimitedMages.Utilities;
using Object = UnityEngine.Object;

namespace UnlimitedMages.Patches;

/// <summary>
///     Contains Harmony patches for the <see cref="MainMenuManager" /> class.
///     These patches are responsible for dynamically resizing the lobby UI and fixing related logic.
/// </summary>
[HarmonyPatch(typeof(MainMenuManager))]
public static class MainMenuManagerPatches
{
    private static bool _isUiResized;

    /// <summary>
    ///     This Postfix now subscribes to the network event. The UI will be resized
    ///     only when the correct team size is received from the host.
    /// </summary>
    [HarmonyPatch(GameConstants.MainMenuManager.StartMethod)]
    [HarmonyPostfix]
    public static void Start_Postfix()
    {
        _isUiResized = false;
        NetworkedConfigManager.OnTeamSizeChanged -= OnTeamSizeUpdated; // Prevent duplicate subscriptions
        NetworkedConfigManager.OnTeamSizeChanged += OnTeamSizeUpdated;
    }

    /// <summary>
    ///     This method is called by the OnTeamSizeChanged event from our network manager.
    ///     It ensures the UI is resized with the authoritative team size from the host.
    /// </summary>
    private static void OnTeamSizeUpdated(int teamSize)
    {
        var instance = Object.FindFirstObjectByType<MainMenuManager>();
        if (instance is null || _isUiResized) return;

        UnlimitedMagesPlugin.Log?.LogInfo($"Received team size {teamSize}. Resizing lobby UI elements...");

        var newLobbySize = teamSize * GameConstants.Game.NumTeams;

        // Resize internal data arrays
        AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.BodiesField).SetValue(instance, new GameObject[newLobbySize]);
        AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.PlayerNamesField).SetValue(instance, new string[newLobbySize]);
        AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.PlayerLevelAndRanksField).SetValue(instance, new string[newLobbySize]);

        // Resize UI element arrays
        ResizeUiList(ref instance.team1, teamSize);
        ResizeUiList(ref instance.team2, teamSize);
        ResizeUiList(ref instance.team1rankandleveltext, teamSize);
        ResizeUiList(ref instance.team2rankandleveltext, teamSize);
        ResizeUiList(ref instance.texts, newLobbySize * 2);
        ResizeUiList(ref instance.rankandleveltext, newLobbySize);
        ResizeAndWrapHats(ref instance.hats, newLobbySize);

        _isUiResized = true;
    }

    /// <summary>
    ///     Resizes a UI Text array by instantiating new elements based on a template.
    ///     It also dynamically adjusts the position and scale of all elements to fit them
    ///     into the same parent container space.
    /// </summary>
    private static void ResizeUiList(ref Text[] array, int newSize)
    {
        // Ensure the array has enough capacity for the new size.
        if (array.Length < newSize)
        {
            if (array.Length < 1) return;
            var originalLength = array.Length;
            var newArray = new Text[newSize];
            Array.Copy(array, newArray, originalLength);
            var template = array[0];
            var parent = template.transform.parent;
            if (parent is null) return;

            for (var i = originalLength; i < newSize; i++)
            {
                var newUiElement = Object.Instantiate(template, parent);
                newUiElement.name = $"{template.name}_Clone_{i}";
                newUiElement.text = "";
                newArray[i] = newUiElement;
            }

            array = newArray;
        }

        // Reposition all elements and hide any that are not needed.
        if (array.Length > 0)
        {
            var originalTeamSizeForLayout = GameConstants.Game.OriginalTeamSize;
            var originalOffset = array.Length > 1 ? array[1].transform.localPosition - array[0].transform.localPosition : new Vector3(0, -35f, 0);

            var scaleFactor = Mathf.Clamp((float)originalTeamSizeForLayout / newSize, 0.5f, 1.0f);
            var newOffset = originalOffset * scaleFactor;
            var basePosition = array[0].transform.localPosition;

            for (var i = 0; i < array.Length; i++)
            {
                var shouldBeActive = i < newSize;
                array[i].gameObject.SetActive(shouldBeActive);

                if (!shouldBeActive) continue;

                array[i].transform.localPosition = basePosition + newOffset * i;
                array[i].transform.localScale = Vector3.one * scaleFactor;
            }
        }
    }

    /// <summary>
    ///     Resizes the 'hats' GameObject array and arranges the new objects in a grid-like
    ///     fashion, wrapping them to new rows to avoid visual clutter in the lobby.
    /// </summary>
    private static void ResizeAndWrapHats(ref GameObject[] array, int newSize)
    {
        // Ensure the array has enough capacity for the new size.
        if (array.Length < newSize)
        {
            if (array.Length < 1) return;
            var originalLength = array.Length;
            var newArray = new GameObject[newSize];
            Array.Copy(array, newArray, originalLength);
            var template = array[0];
            var parent = template.transform.parent;
            if (parent is null) return;
            var horizontalOffset = array.Length > 1 ? array[1].transform.position - array[0].transform.position : Vector3.zero;
            var verticalOffset = new Vector3(0, horizontalOffset.magnitude * 2.5f, 0);

            for (var i = originalLength; i < newSize; i++)
            {
                var column = i % GameConstants.Game.OriginalTeamSize;
                var row = i / GameConstants.Game.OriginalTeamSize;
                var basePosition = array[column].transform.position;
                var newHat = Object.Instantiate(template, parent);
                newHat.name = $"{template.name}_Clone_{i}";
                var finalPosition = basePosition + verticalOffset * row;
                if (row % 2 != 0) finalPosition += horizontalOffset * 0.5f;

                newHat.transform.position = finalPosition;
                newArray[i] = newHat;
            }

            array = newArray;
        }

        // Hide any hat slots that are not needed for the current team size.
        for (var i = 0; i < array.Length; i++)
            array[i].SetActive(i < newSize);
    }

    /// <summary>
    ///     Transpiles map selection methods to use the dynamic lobby size for setting member limits.
    /// </summary>
    [HarmonyPatch(nameof(MainMenuManager.SmallMap))]
    [HarmonyPatch(nameof(MainMenuManager.LargeMap))]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> MapSelect_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);

        var getInstance = AccessTools.PropertyGetter(typeof(NetworkedConfigManager), nameof(NetworkedConfigManager.Instance));
        var getTeamSize = AccessTools.PropertyGetter(typeof(NetworkedConfigManager), nameof(NetworkedConfigManager.TeamSize));

        for (var i = 0; i < newInstructions.Count; i++)
        {
            // Find where the hardcoded lobby size (8) is loaded.
            if (newInstructions[i].opcode != OpCodes.Ldc_I4_8) continue;

            // Replace it with a call to get the dynamic total lobby size.
            newInstructions[i] = new CodeInstruction(OpCodes.Call, getInstance);
            newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, getTeamSize));
            newInstructions.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_I4, GameConstants.Game.NumTeams));
            newInstructions.Insert(i + 3, new CodeInstruction(OpCodes.Mul));
        }

        return newInstructions;
    }

    /// <summary>
    ///     Transpiles SyncHats to fix a bug where it uses an unclamped player name when
    ///     adding to the kick dictionary, causing a mismatch later.
    /// </summary>
    [HarmonyPatch(GameConstants.MainMenuManager.SyncHatsMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> SyncHats_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var addToDictMethod = AccessTools.Method(typeof(KickPlayersHolder), "AddToDict");
        var clampStringMethod = AccessTools.Method(typeof(MainMenuManager), GameConstants.MainMenuManager.ClampStringMethod);
        for (var i = 0; i < newInstructions.Count; i++)
        {
            if (!newInstructions[i].Calls(addToDictMethod)) continue;

            var instructionsToInsert = new List<CodeInstruction>
                { new(OpCodes.Ldarg_0), new(OpCodes.Ldarg_1), new(OpCodes.Ldc_I4, GameConstants.MainMenuManager.PlayerNameClampLength), new(OpCodes.Call, clampStringMethod) };

            newInstructions.RemoveAt(i - 2);
            newInstructions.InsertRange(i - 2, instructionsToInsert);
            break;
        }

        return newInstructions;
    }

    /// <summary>
    ///     Prefixes the RemoveHat method to correctly clear all UI elements and GameObjects
    ///     associated with a player, using the dynamically resized arrays.
    /// </summary>
    [HarmonyPatch(GameConstants.MainMenuManager.RemoveHatMethod)]
    [HarmonyPrefix]
    public static bool RemoveHat_Prefix(MainMenuManager __instance, string PlayerName)
    {
        var playerNamesField = AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.PlayerNamesField);
        var bodiesField = AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.BodiesField);
        string[] playerNames = (string[])playerNamesField.GetValue(__instance);
        GameObject[] bodies = (GameObject[])bodiesField.GetValue(__instance);
        for (var i = 0; i < playerNames.Length; i++)
        {
            if (playerNames[i] != PlayerName) continue;

            if (bodies != null && i < bodies.Length && bodies[i] != null)
            {
                Object.Destroy(bodies[i]);
                bodies[i] = null!;
            }

            if (__instance.hats != null && i < __instance.hats.Length) __instance.hats[i].SetActive(false);
            if (__instance.rankandleveltext != null && i < __instance.rankandleveltext.Length) __instance.rankandleveltext[i].text = "";
            if (__instance.texts != null && i * 2 + 1 < __instance.texts.Length)
            {
                __instance.texts[i * 2].text = "";
                __instance.texts[i * 2 + 1].text = "";
            }

            break;
        }

        // Allow the original method to run, but return true instead of false to let it proceed.
        // The original method has some additional logic.
        return true;
    }

    /// <summary>
    ///     Postfixes RemoveHat to ensure the player is also removed from the kick dictionary,
    ///     using the correctly clamped name. This fixes a bug where disconnected players
    ///     couldn't be kicked.
    /// </summary>
    [HarmonyPatch(GameConstants.MainMenuManager.RemoveHatMethod)]
    [HarmonyPostfix]
    public static void RemoveHat_Postfix(MainMenuManager __instance, string PlayerName)
    {
        var clampStringMethod = AccessTools.Method(typeof(MainMenuManager), GameConstants.MainMenuManager.ClampStringMethod);
        var clampedName = (string)clampStringMethod.Invoke(__instance, new object[] { PlayerName, GameConstants.MainMenuManager.PlayerNameClampLength });

        var kickDictionaryField = AccessTools.Field(typeof(KickPlayersHolder), "nametosteamid");
        var kickDictionary = (Dictionary<string, string>)kickDictionaryField.GetValue(__instance.kickplayershold);

        if (kickDictionary != null && kickDictionary.ContainsKey(clampedName))
            kickDictionary.Remove(clampedName);
    }

    /// <summary>
    ///     Prefixes the LeaveLobby button call to redirect to our improved cleanup logic,
    ///     ensuring a smoother exit from lobbies.
    /// </summary>
    [HarmonyPatch(nameof(MainMenuManager.LeaveLobby))]
    [HarmonyPrefix]
    public static bool LeaveLobby_Prefix()
    {
        BootstrapManager.LeaveLobby2();
        return false; // Skip original method
    }
}
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnlimitedMages.System;
using UnlimitedMages.Utilities;
using UnityEngine;
using UnityEngine.UI;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using Object = UnityEngine.Object;

namespace UnlimitedMages.Patches;

[HarmonyPatch(typeof(MainMenuManager))]
public static class MainMenuManagerPatches
{
    private static bool _isLobbyUiReady;
    private static readonly List<(string name, string rank, string steamId)> PendingPlayers = new();
    private static Coroutine? _timeoutCoroutine;

    [HarmonyPatch(GameConstants.MainMenuManager.StartMethod)]
    [HarmonyPostfix]
    public static void Start_Postfix(MainMenuManager __instance)
    {
        _isLobbyUiReady = false;
        PendingPlayers.Clear();

        // Ensure we don't double-subscribe if this object is reused without a scene change.
        EventBus.Unsubscribe<ConfigReadyEvent>(OnConfigReady_ResizeLobbyUI);
        EventBus.Subscribe<ConfigReadyEvent>(OnConfigReady_ResizeLobbyUI);

        if (_timeoutCoroutine != null)
        {
            __instance.StopCoroutine(_timeoutCoroutine);
        }

        _timeoutCoroutine = __instance.StartCoroutine(LobbyUiTimeoutCoroutine());
    }

    private static IEnumerator LobbyUiTimeoutCoroutine()
    {
        yield return new WaitForSeconds(15f);

        if (!_isLobbyUiReady)
        {
            UnlimitedMagesPlugin.Log?.LogWarning("Timed out waiting for host config. Building UI with default size to prevent deadlock.");
            // Manually fire a config ready event with the default size
            OnConfigReady_ResizeLobbyUI(new ConfigReadyEvent(GameConstants.Game.OriginalTeamSize));
        }
    }

    private static void OnConfigReady_ResizeLobbyUI(ConfigReadyEvent evt)
    {
        var instance = Object.FindFirstObjectByType<MainMenuManager>();
        if (instance == null || _isLobbyUiReady) return;

        var teamSize = evt.TeamSize;
        UnlimitedMagesPlugin.Log?.LogInfo($"Received team size {teamSize}. Resizing lobby UI elements...");

        var newLobbySize = teamSize * GameConstants.Game.NumTeams;

        AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.BodiesField)
            .SetValue(instance, new GameObject[newLobbySize]);
        AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.PlayerNamesField)
            .SetValue(instance, new string[newLobbySize]);
        AccessTools.Field(typeof(MainMenuManager), GameConstants.MainMenuManager.PlayerLevelAndRanksField)
            .SetValue(instance, new string[newLobbySize]);

        ResizeUiList(ref instance.team1, teamSize);
        ResizeUiList(ref instance.team2, teamSize);
        ResizeUiList(ref instance.team1rankandleveltext, teamSize);
        ResizeUiList(ref instance.team2rankandleveltext, teamSize);
        ResizeUiList(ref instance.texts, newLobbySize * 2);
        ResizeUiList(ref instance.rankandleveltext, newLobbySize);
        ResizeAndWrapHats(ref instance.hats, newLobbySize);

        _isLobbyUiReady = true;

        UnlimitedMagesPlugin.Log?.LogInfo($"Processing {PendingPlayers.Count} pending players...");
        foreach (var player in PendingPlayers)
        {
            instance.SyncHats(player.name, player.rank, player.steamId);
        }

        PendingPlayers.Clear();
    }

    private static void ResizeUiList(ref Text[] array, int newSize)
    {
        if (array.Length >= newSize) return;
        if (array.Length < 1) return;

        var originalLength = array.Length;
        var newArray = new Text[newSize];
        Array.Copy(array, newArray, originalLength);

        var template = array[0];
        var parent = template.transform.parent;
        if (parent == null) return;

        for (var i = originalLength; i < newSize; i++)
        {
            var newUiElement = Object.Instantiate(template, parent);
            newUiElement.name = $"{template.name}_Clone_{i}";
            newUiElement.text = "";
            newArray[i] = newUiElement;
        }

        array = newArray;

        if (array.Length <= 0) return;

        var originalTeamSizeForLayout = GameConstants.Game.OriginalTeamSize;
        var originalOffset = array.Length > 1
            ? array[1].transform.localPosition - array[0].transform.localPosition
            : new Vector3(0, -35f, 0);

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

    private static void ResizeAndWrapHats(ref GameObject[] array, int newSize)
    {
        if (array.Length >= newSize)
        {
            for (var i = 0; i < array.Length; i++)
                array[i].SetActive(i < newSize);
            return;
        }

        if (array.Length < 1) return;
        var originalLength = array.Length;
        var newArray = new GameObject[newSize];
        Array.Copy(array, newArray, originalLength);

        var template = array[0];
        var parent = template.transform.parent;
        if (parent == null) return;

        var horizontalOffset = array.Length > 1
            ? array[1].transform.position - array[0].transform.position
            : Vector3.zero;
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

        for (var i = 0; i < array.Length; i++)
            array[i].SetActive(i < newSize);
    }

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
            if (newInstructions[i].opcode != OpCodes.Ldc_I4_8) continue;

            newInstructions[i] = new CodeInstruction(OpCodes.Call, getInstance);
            newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, getTeamSize));
            newInstructions.Insert(i + 2, new CodeInstruction(OpCodes.Ldc_I4, GameConstants.Game.NumTeams));
            newInstructions.Insert(i + 3, new CodeInstruction(OpCodes.Mul));
        }

        return newInstructions;
    }

    [HarmonyPatch(GameConstants.MainMenuManager.SyncHatsMethod)]
    [HarmonyPrefix]
    public static bool SyncHats_Prefix(string PlayerName, string PlayerRank, string steamid)
    {
        if (_isLobbyUiReady)
        {
            return true; // UI is ready, proceed to original method.
        }

        PendingPlayers.Add((PlayerName, PlayerRank, steamid));
        return false; // UI not ready, queue the player and skip original method.
    }

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
            {
                new(OpCodes.Ldarg_0),
                new(OpCodes.Ldarg_1),
                new(OpCodes.Ldc_I4, GameConstants.MainMenuManager.PlayerNameClampLength),
                new(OpCodes.Call, clampStringMethod)
            };

            newInstructions.RemoveAt(i - 2);
            newInstructions.InsertRange(i - 2, instructionsToInsert);
            break;
        }

        return newInstructions;
    }

    [HarmonyPatch(GameConstants.MainMenuManager.RemoveHatMethod)]
    [HarmonyPostfix]
    public static void RemoveHat_Postfix(MainMenuManager __instance, string PlayerName)
    {
        var clampStringMethod = AccessTools.Method(typeof(MainMenuManager), GameConstants.MainMenuManager.ClampStringMethod);
        var clampedName = (string)clampStringMethod.Invoke(__instance, new object[] { PlayerName, GameConstants.MainMenuManager.PlayerNameClampLength });

        var kickDictionaryField = AccessTools.Field(typeof(KickPlayersHolder), "nametosteamid");
        var kickDictionary = (Dictionary<string, string>)kickDictionaryField.GetValue(__instance.kickplayershold);

        if (kickDictionary != null && kickDictionary.ContainsKey(clampedName))
        {
            kickDictionary.Remove(clampedName);
        }
    }
}
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnlimitedMages.System.Components;
using UnlimitedMages.Utilities;
using Object = UnityEngine.Object;

namespace UnlimitedMages.Patches;

/// <summary>
///     Contains Harmony patches for the <see cref="PlayerRespawnManager" /> class.
///     These patches adjust in-game UI, scoring, and announcer sounds to work with dynamic team sizes.
/// </summary>
[HarmonyPatch(typeof(PlayerRespawnManager))]
internal static class PlayerRespawnManagerPatches
{
    /// <summary>
    ///     Postfixes the OnStartClient method to dynamically resize in-game UI elements
    ///     based on the final lobby size.
    /// </summary>
    [HarmonyPatch(GameConstants.PlayerRespawnManager.OnStartClientMethod)]
    [HarmonyPostfix]
    public static void OnStartClient_Postfix(PlayerRespawnManager __instance)
    {
        if (ConfigManager.Instance == null || !ConfigManager.Instance.IsConfigReady) return;

        var teamSize = ConfigManager.Instance.TeamSize;
        var newLobbySize = teamSize * GameConstants.Game.NumTeams;

        // Resize and recalculate the positions for the "death message" UI list.
        var positionsField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.PositionsField);
        var originalPositions = (float[])positionsField.GetValue(__instance);

        if (originalPositions.Length == newLobbySize) return;

        var newPositions = new float[newLobbySize];
        var firstPos = GameConstants.PlayerRespawnManager.DeathMessageFirstY;
        var lastPos = GameConstants.PlayerRespawnManager.DeathMessageLastY;
        var totalRange = firstPos - lastPos;
        var newSpacing = totalRange / (newLobbySize - 1);

        for (var i = 0; i < newLobbySize; i++) newPositions[i] = firstPos - i * newSpacing;

        positionsField.SetValue(__instance, newPositions);

        // Update the internal field that tracks the number of players per team.
        var warlockssetField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.WarlocksSetField);
        warlockssetField.SetValue(__instance, teamSize);
    }

    /// <summary>
    ///     Transpiles the FadeInVignette method to replace a hardcoded team size of 4 with the dynamic team size.
    ///     This affects game logic related to round starts.
    /// </summary>
    [HarmonyPatch(GameConstants.PlayerRespawnManager.FadeInVignetteMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> FadeInVignette_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var warlocksSetField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.WarlocksSetField);

        for (var i = 0; i < newInstructions.Count; i++)
        {
            // Find the pattern where the 'warlocksset' field is being set to 4.
            if (i + 1 >= newInstructions.Count ||
                newInstructions[i].opcode != OpCodes.Ldc_I4_4 ||
                newInstructions[i + 1].opcode != OpCodes.Stfld ||
                newInstructions[i + 1].operand as FieldInfo != warlocksSetField) continue;

            // Replace the hardcoded 4 with a dynamic call to get the configured team size.
            var getInstance = AccessTools.PropertyGetter(typeof(ConfigManager), nameof(ConfigManager.Instance));
            newInstructions[i] = new CodeInstruction(OpCodes.Call, getInstance);

            var getTeamSize = AccessTools.PropertyGetter(typeof(ConfigManager), nameof(ConfigManager.TeamSize));
            newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, getTeamSize));

            UnlimitedMagesPlugin.Log?.LogInfo("Successfully transpiled FadeInVignette to use dynamic team size.");
            break;
        }

        return newInstructions;
    }

    /// <summary>
    ///     Postfixes the EndGame method to ensure the end-of-game scoreboard has enough slots for all players.
    ///     It duplicates the template scoreboard panel if necessary.
    /// </summary>
    [HarmonyPatch(GameConstants.PlayerRespawnManager.EndGameMethod)]
    [HarmonyPostfix]
    public static void EndGame_Postfix(PlayerRespawnManager __instance)
    {
        var scoreboardField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.ScoreboardField);
        var scoreboard = (GameObject)scoreboardField.GetValue(__instance);
        if (scoreboard == null) return;

        if (ConfigManager.Instance?.TeamSize == null) return;
        var requiredChildCount = ConfigManager.Instance.TeamSize * GameConstants.Game.NumTeams;

        var scoreboardTransform = scoreboard.transform;
        var currentChildCount = scoreboardTransform.childCount;
        if (currentChildCount >= requiredChildCount) return;
        if (currentChildCount == 0) return; // No template to clone.

        var template = scoreboardTransform.GetChild(0).gameObject;

        // Instantiate new scoreboard panels to meet the required count.
        for (var i = currentChildCount; i < requiredChildCount; i++)
        {
            var newPanel = Object.Instantiate(template, scoreboardTransform);
            newPanel.name = $"{template.name}_Clone_{i}";
        }
    }

    /// <summary>
    ///     Prefixes the announcer sound method to implement custom logic for playing death callouts.
    ///     The original method is hardcoded for 4 players; this version counts dead players per team and plays the correct sound.
    /// </summary>
    [HarmonyPatch(GameConstants.PlayerRespawnManager.PlayAnnouncerSoundMethod)]
    [HarmonyPrefix]
    public static bool PlayAnnouncerSound_Prefix(PlayerRespawnManager __instance, int pteam)
    {
        var deadPlayersField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.DeadPlayersField);
        var deadPlayers = (List<GameObject>)deadPlayersField.GetValue(__instance);

        // Count the number of dead players on each team.
        var team1Deaths = 0;
        var team2Deaths = 0;
        foreach (var deadPlayer in deadPlayers)
        {
            if (deadPlayer == null || !deadPlayer.TryGetComponent<PlayerMovement>(out var pm)) continue;
            switch (pm.playerTeam)
            {
                case 0:
                    team1Deaths++;
                    break;
                case 2:
                    team2Deaths++;
                    break;
            }
        }

        var serverPaSoundMethod = AccessTools.Method(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.ServerPAsoundMethod);

        // Determine which announcer clip to play based on the death count.
        // The clip index is clamped to the original range (0-3) to avoid errors with the announcer's sound array.
        switch (pteam)
        {
            case 0 when team1Deaths > 0:
            {
                var clipIndex = Mathf.Clamp(team1Deaths - 1, 0, GameConstants.Game.OriginalTeamSize - 1);
                serverPaSoundMethod.Invoke(__instance, [clipIndex]);
                break;
            }
            case 2 when team2Deaths > 0:
            {
                var clipIndex = Mathf.Clamp(team2Deaths - 1, 0, GameConstants.Game.OriginalTeamSize - 1);
                // The second team's sounds are offset in the audio clip array.
                serverPaSoundMethod.Invoke(__instance, [clipIndex + GameConstants.Game.OriginalTeamSize]);
                break;
            }
        }

        // Replicate original logic to clear the dead player list in Colosseum mode.
        if ((bool)AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.IsColosseumField).GetValue(__instance)) deadPlayers.Clear();

        return false; // Prevent the original method from running.
    }
}
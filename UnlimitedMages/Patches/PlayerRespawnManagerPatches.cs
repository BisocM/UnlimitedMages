using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnlimitedMages.Networking;
using UnlimitedMages.Utilities;
using Object = UnityEngine.Object;

namespace UnlimitedMages.Patches;

/// <summary>
///     Contains Harmony patches for the <see cref="PlayerRespawnManager" /> class.
///     These patches adjust in-game UI, game logic, and announcer sounds for larger player counts.
/// </summary>
[HarmonyPatch(typeof(PlayerRespawnManager))]
public static class PlayerRespawnManagerPatches
{
    /// <summary>
    ///     Postfixes OnStartClient to resize UI element arrays and update game logic variables.
    /// </summary>
    [HarmonyPatch(GameConstants.PlayerRespawnManager.OnStartClientMethod)]
    [HarmonyPostfix]
    public static void OnStartClient_Postfix(PlayerRespawnManager __instance)
    {
        if (NetworkedConfigManager.Instance == null) return;
        var teamSize = NetworkedConfigManager.Instance.TeamSize;
        var newLobbySize = teamSize * GameConstants.Game.NumTeams;

        var positionsField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.PositionsField);
        var originalPositions = (float[])positionsField.GetValue(__instance);

        if (originalPositions.Length == newLobbySize) return;

        // Dynamically recalculate the vertical positions for death messages in the kill feed.
        var newPositions = new float[newLobbySize];
        var firstPos = GameConstants.PlayerRespawnManager.DeathMessageFirstY;
        var lastPos = GameConstants.PlayerRespawnManager.DeathMessageLastY;
        var totalRange = firstPos - lastPos;
        var newSpacing = totalRange / (newLobbySize - 1);

        for (var i = 0; i < newLobbySize; i++) newPositions[i] = firstPos - i * newSpacing;

        positionsField.SetValue(__instance, newPositions);

        var warlockssetField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.WarlocksSetField);
        warlockssetField.SetValue(__instance, teamSize);
    }

    /// <summary>
    ///     Transpiles FadeInVignette to replace a hardcoded team size value.
    /// </summary>
    [HarmonyPatch(GameConstants.PlayerRespawnManager.FadeInVignetteMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> FadeInVignette_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var warlocksSetField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.WarlocksSetField);

        for (var i = 0; i < newInstructions.Count; i++)
        {
            // We are looking for the IL instruction that loads the constant number 4 onto the stack (ldc.i4.4)
            // right before it's stored in the 'warlocksset' field (stfld).
            if (i + 1 >= newInstructions.Count ||
                newInstructions[i].opcode != OpCodes.Ldc_I4_4 ||
                newInstructions[i + 1].opcode != OpCodes.Stfld ||
                newInstructions[i + 1].operand as FieldInfo != warlocksSetField) continue;

            var getInstance = AccessTools.PropertyGetter(typeof(NetworkedConfigManager), nameof(NetworkedConfigManager.Instance));
            newInstructions[i] = new CodeInstruction(OpCodes.Call, getInstance);

            var getTeamSize = AccessTools.PropertyGetter(typeof(NetworkedConfigManager), nameof(NetworkedConfigManager.TeamSize));
            newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, getTeamSize));

            UnlimitedMagesPlugin.Log?.LogInfo("Successfully transpiled FadeInVignette to use dynamic team size.");
            break;
        }

        return newInstructions;
    }

    /// <summary>
    ///     Postfixes EndGame to dynamically add more player panels to the scoreboard UI.
    /// </summary>
    [HarmonyPatch(GameConstants.PlayerRespawnManager.EndGameMethod)]
    [HarmonyPostfix]
    public static void EndGame_Postfix(PlayerRespawnManager __instance)
    {
        var scoreboardField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.ScoreboardField);
        var scoreboard = (GameObject)scoreboardField.GetValue(__instance);
        if (scoreboard == null) return;

        if (NetworkedConfigManager.Instance?.TeamSize == null) return;
        var requiredChildCount = NetworkedConfigManager.Instance.TeamSize * GameConstants.Game.NumTeams;

        var scoreboardTransform = scoreboard.transform;
        var currentChildCount = scoreboardTransform.childCount;
        if (currentChildCount >= requiredChildCount) return;
        if (currentChildCount == 0) return;

        var template = scoreboardTransform.GetChild(0).gameObject;

        for (var i = currentChildCount; i < requiredChildCount; i++)
        {
            var newPanel = Object.Instantiate(template, scoreboardTransform);
            newPanel.name = $"{template.name}_Clone_{i}";
        }
    }

    /// <summary>
    ///     Prefixes PlayAnnouncerSound to implement custom logic for handling multi-kill sounds.
    ///     This prevents errors when the number of deaths exceeds the number of available announcer clips.
    /// </summary>
    /// <returns>Returns false to prevent the original method from running.</returns>
    [HarmonyPatch(GameConstants.PlayerRespawnManager.PlayAnnouncerSoundMethod)]
    [HarmonyPrefix]
    public static bool PlayAnnouncerSound_Prefix(PlayerRespawnManager __instance, int pteam)
    {
        var deadPlayersField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.DeadPlayersField);
        var deadPlayers = (List<GameObject>)deadPlayersField.GetValue(__instance);

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

        switch (pteam)
        {
            case 0 when team1Deaths > 0:
            {
                // Clamp the clip index to the maximum available in the base game (0-3 for a quad kill)
                // to prevent an IndexOutOfRangeException. The 'quad kill' sound will play for any subsequent kills.
                var clipIndex = Mathf.Clamp(team1Deaths - 1, 0, GameConstants.Game.OriginalTeamSize - 1);
                serverPaSoundMethod.Invoke(__instance, [clipIndex]);
                break;
            }
            case 2 when team2Deaths > 0:
            {
                var clipIndex = Mathf.Clamp(team2Deaths - 1, 0, GameConstants.Game.OriginalTeamSize - 1);
                serverPaSoundMethod.Invoke(__instance, [clipIndex + GameConstants.Game.OriginalTeamSize]);
                break;
            }
        }

        if ((bool)AccessTools.Field(typeof(PlayerRespawnManager), "iscolosseum").GetValue(__instance)) deadPlayers.Clear();

        return false;
    }
}
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnlimitedMages.System.Components;
using UnlimitedMages.Utilities;
using Object = UnityEngine.Object;

namespace UnlimitedMages.Patches;

[HarmonyPatch(typeof(PlayerRespawnManager))]
internal static class PlayerRespawnManagerPatches
{
    [HarmonyPatch(GameConstants.PlayerRespawnManager.OnStartClientMethod)]
    [HarmonyPostfix]
    public static void OnStartClient_Postfix(PlayerRespawnManager __instance)
    {
        // Read configuration from the centralized manager
        if (ConfigManager.Instance == null || !ConfigManager.Instance.IsConfigReady) return;

        var teamSize = ConfigManager.Instance.TeamSize;
        var newLobbySize = teamSize * GameConstants.Game.NumTeams;

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

        var warlockssetField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.WarlocksSetField);
        warlockssetField.SetValue(__instance, teamSize);
    }

    [HarmonyPatch(GameConstants.PlayerRespawnManager.FadeInVignetteMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> FadeInVignette_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var warlocksSetField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.WarlocksSetField);

        for (var i = 0; i < newInstructions.Count; i++)
        {
            if (i + 1 >= newInstructions.Count ||
                newInstructions[i].opcode != OpCodes.Ldc_I4_4 ||
                newInstructions[i + 1].opcode != OpCodes.Stfld ||
                newInstructions[i + 1].operand as FieldInfo != warlocksSetField) continue;

            // Get the team size from the new centralized ConfigManager
            var getInstance = AccessTools.PropertyGetter(typeof(ConfigManager), nameof(ConfigManager.Instance));
            newInstructions[i] = new CodeInstruction(OpCodes.Call, getInstance);

            var getTeamSize = AccessTools.PropertyGetter(typeof(ConfigManager), nameof(ConfigManager.TeamSize));
            newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, getTeamSize));

            UnlimitedMagesPlugin.Log?.LogInfo("Successfully transpiled FadeInVignette to use dynamic team size.");
            break;
        }

        return newInstructions;
    }

    [HarmonyPatch(GameConstants.PlayerRespawnManager.EndGameMethod)]
    [HarmonyPostfix]
    public static void EndGame_Postfix(PlayerRespawnManager __instance)
    {
        var scoreboardField = AccessTools.Field(typeof(PlayerRespawnManager), GameConstants.PlayerRespawnManager.ScoreboardField);
        var scoreboard = (GameObject)scoreboardField.GetValue(__instance);
        if (scoreboard == null) return;

        // Read configuration from the centralized manager
        if (ConfigManager.Instance?.TeamSize == null) return;
        var requiredChildCount = ConfigManager.Instance.TeamSize * GameConstants.Game.NumTeams;

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
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using UnlimitedMages.System.Components;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.Patches;

[HarmonyPatch(typeof(MainMenuManagerNetworked))]
internal static class MainMenuManagerNetworkedPatches
{
    static MainMenuManagerNetworkedPatches()
    {
        EventBus.Subscribe<ConfigReadyEvent>(OnConfigReady_ResizeTeamArrays);
    }

    #region Patches

    [HarmonyPatch(GameConstants.MainMenuManagerNetworked.ResetLocalTeamMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ResetLocalTeam_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var modifiedInstructions = TranspileResizeTeamArrays(instructions, GameConstants.MainMenuManagerNetworked.Team1PlayersField);
        return TranspileResizeTeamArrays(modifiedInstructions, GameConstants.MainMenuManagerNetworked.Team2PlayersField);
    }

    [HarmonyPatch(GameConstants.MainMenuManagerNetworked.StartMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Start_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // Chain the transpiler to modify both team arrays within the same method
        var modifiedInstructions = TranspileResizeTeamArrays(instructions, GameConstants.MainMenuManagerNetworked.Team1PlayersField);
        return TranspileResizeTeamArrays(modifiedInstructions, GameConstants.MainMenuManagerNetworked.Team2PlayersField);
    }

    #endregion

    #region Helpers

    private static void OnConfigReady_ResizeTeamArrays(ConfigReadyEvent evt)
    {
        var instance = Object.FindFirstObjectByType<MainMenuManagerNetworked>();
        if (instance == null) return;

        var teamSize = evt.TeamSize;
        UnlimitedMagesPlugin.Log?.LogInfo($"Session config ready. Resizing MainMenuManagerNetworked arrays to size {teamSize}.");

        var team1Field = AccessTools.Field(typeof(MainMenuManagerNetworked), GameConstants.MainMenuManagerNetworked.Team1PlayersField);
        var team2Field = AccessTools.Field(typeof(MainMenuManagerNetworked), GameConstants.MainMenuManagerNetworked.Team2PlayersField);

        team1Field.SetValue(instance, new string[teamSize]);
        team2Field.SetValue(instance, new string[teamSize]);
    }

    private static IEnumerable<CodeInstruction> TranspileResizeTeamArrays(IEnumerable<CodeInstruction> instructions, string targetFieldName)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var targetField = AccessTools.Field(typeof(MainMenuManagerNetworked), targetFieldName);

        var getInstance = AccessTools.PropertyGetter(typeof(ConfigManager), nameof(ConfigManager.Instance));
        var getTeamSize = AccessTools.PropertyGetter(typeof(ConfigManager), nameof(ConfigManager.TeamSize));

        for (var i = 0; i < newInstructions.Count; i++)
        {
            // Looking for the pattern:
            // ldc.i4.4                  (Load constant integer 4)
            // newarr System.String      (Create a new string array of that size)
            // stfld string[] ...        (Store it in our target field)
            if (i + 2 >= newInstructions.Count ||
                newInstructions[i].opcode != OpCodes.Ldc_I4_4 ||
                newInstructions[i + 1].opcode != OpCodes.Newarr ||
                newInstructions[i + 2].opcode != OpCodes.Stfld ||
                newInstructions[i + 2].operand as FieldInfo != targetField) continue;

            UnlimitedMagesPlugin.Log?.LogInfo($"Transpiling {targetFieldName} initialization...");

            // Replace 'ldc.i4.4' with a call to get the dynamic team size
            newInstructions[i] = new CodeInstruction(OpCodes.Call, getInstance);
            newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, getTeamSize));

            UnlimitedMagesPlugin.Log?.LogInfo($"Successfully patched {targetFieldName} size.");
            break;
        }

        return newInstructions;
    }

    #endregion
}
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Steamworks;
using UnlimitedMages.System.Components;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using UnlimitedMages.Utilities;
using Object = UnityEngine.Object;

namespace UnlimitedMages.Patches;

/// <summary>
///     Contains Harmony patches for the <see cref="MainMenuManagerNetworked" /> class.
///     These patches handle networked team management, resizing team data structures, and intercepting RPCs.
/// </summary>
[HarmonyPatch(typeof(MainMenuManagerNetworked))]
internal static class MainMenuManagerNetworkedPatches
{
    // Cached reflection info for performance.
    private static readonly FieldInfo? MmmField = AccessTools.Field(typeof(MainMenuManagerNetworked), GameConstants.MainMenuManagerNetworked.MmmField);
    private static readonly FieldInfo? LocalPlayerNameField = AccessTools.Field(typeof(MainMenuManagerNetworked), GameConstants.MainMenuManagerNetworked.LocalPlayerNameField);
    private static readonly FieldInfo? CurrentLocalTeamField = AccessTools.Field(typeof(MainMenuManagerNetworked), GameConstants.MainMenuManagerNetworked.CurrentLocalTeamField);

    static MainMenuManagerNetworkedPatches()
    {
        // Subscribe to the config ready event to resize arrays as soon as the team size is known.
        EventBus.Subscribe<ConfigReadyEvent>(OnConfigReady_ResizeTeamArrays);
    }

    /// <summary>
    ///     Transpiles the ResetLocalTeam method to initialize team arrays with the dynamic team size.
    /// </summary>
    [HarmonyPatch(GameConstants.MainMenuManagerNetworked.ResetLocalTeamMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> ResetLocalTeam_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var modifiedInstructions = TranspileResizeTeamArrays(instructions, GameConstants.MainMenuManagerNetworked.Team1PlayersField);
        return TranspileResizeTeamArrays(modifiedInstructions, GameConstants.MainMenuManagerNetworked.Team2PlayersField);
    }

    /// <summary>
    ///     Transpiles the Start method to initialize team arrays with the dynamic team size.
    /// </summary>
    [HarmonyPatch(GameConstants.MainMenuManagerNetworked.StartMethod)]
    [HarmonyTranspiler]
    public static IEnumerable<CodeInstruction> Start_Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var modifiedInstructions = TranspileResizeTeamArrays(instructions, GameConstants.MainMenuManagerNetworked.Team1PlayersField);
        return TranspileResizeTeamArrays(modifiedInstructions, GameConstants.MainMenuManagerNetworked.Team2PlayersField);
    }

    /// <summary>
    ///     Prefixes the game's internal RPC for a player joining a team.
    ///     This intercepts the call, prevents the original logic, and instead updates the mod's custom <see cref="LobbyStateManager" />.
    /// </summary>
    [HarmonyPatch(GameConstants.MainMenuManagerNetworked.ObserversJoinTeamRpc)]
    [HarmonyPrefix]
    public static bool ObserversJoinTeam_Logic_Prefix(MainMenuManagerNetworked __instance, string playername, int teamtojoin, int index, string lvlandrank)
    {
        var isForMe = playername == SteamFriends.GetPersonaName();

        // Update local player's team information if this RPC is for them.
        if (isForMe)
            if (MmmField?.GetValue(__instance) is MainMenuManager mmm && CurrentLocalTeamField != null)
            {
                mmm.hasSwappedteam = true;
                CurrentLocalTeamField.SetValue(__instance, teamtojoin);
                if (mmm.pm != null)
                    mmm.pm.playerTeam = teamtojoin;

                BootstrapManager.instance.currentTeam = teamtojoin;
            }

        // Update the central lobby state with the new team assignment.
        LobbyStateManager.Instance?.AssignPlayerToTeam(teamtojoin, index, playername, lvlandrank);

        return false; // Prevents the original game method from executing.
    }

    /// <summary>
    ///     Prefixes the game's internal RPC for removing a player from a team.
    ///     This intercepts the call, prevents the original logic, and instead updates the mod's custom <see cref="LobbyStateManager" />.
    /// </summary>
    [HarmonyPatch(GameConstants.MainMenuManagerNetworked.ObsRemoveFromTeamRpc)]
    [HarmonyPrefix]
    public static bool ObsRemoveFromTeam_Logic_Prefix(int team, int index)
    {
        LobbyStateManager.Instance?.ClearPlayerFromTeamSlot(team, index);
        return false; // Prevents the original game method from executing.
    }

    /// <summary>
    ///     Event handler that resizes the internal team player arrays when the mod configuration is ready.
    ///     This is a fallback for when the transpilers might not run before the object is created.
    /// </summary>
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

    /// <summary>
    ///     A generic transpiler that finds where a team player array is initialized (e.g., `new string[4]`)
    ///     and replaces the hardcoded size with a dynamic call to <see cref="ConfigManager.TeamSize" />.
    /// </summary>
    private static IEnumerable<CodeInstruction> TranspileResizeTeamArrays(IEnumerable<CodeInstruction> instructions, string targetFieldName)
    {
        var newInstructions = new List<CodeInstruction>(instructions);
        var targetField = AccessTools.Field(typeof(MainMenuManagerNetworked), targetFieldName);

        var getInstance = AccessTools.PropertyGetter(typeof(ConfigManager), nameof(ConfigManager.Instance));
        var getTeamSize = AccessTools.PropertyGetter(typeof(ConfigManager), nameof(ConfigManager.TeamSize));

        for (var i = 0; i < newInstructions.Count; i++)
        {
            // Find the pattern: ldc.i4.4, newarr, stfld (where the field matches our target).
            if (i + 2 >= newInstructions.Count ||
                newInstructions[i].opcode != OpCodes.Ldc_I4_4 ||
                newInstructions[i + 1].opcode != OpCodes.Newarr ||
                newInstructions[i + 2].opcode != OpCodes.Stfld ||
                newInstructions[i + 2].operand as FieldInfo != targetField) continue;

            UnlimitedMagesPlugin.Log?.LogInfo($"Transpiling {targetFieldName} initialization...");

            // Replace `ldc.i4.4` with `call ConfigManager.get_Instance()` and `callvirt ConfigManager.get_TeamSize()`.
            newInstructions[i] = new CodeInstruction(OpCodes.Call, getInstance);
            newInstructions.Insert(i + 1, new CodeInstruction(OpCodes.Callvirt, getTeamSize));

            UnlimitedMagesPlugin.Log?.LogInfo($"Successfully patched {targetFieldName} size.");
            break;
        }

        return newInstructions;
    }
}
using HarmonyLib;
using UnlimitedMages.System;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.Patches;

[HarmonyPatch(typeof(MainMenuManagerNetworked))]
public static class MainMenuManagerNetworkedPatches
{

    static MainMenuManagerNetworkedPatches()
    {
        EventBus.Subscribe<ConfigReadyEvent>(OnConfigReady_ResizeTeamArrays);
    }

    private static void OnConfigReady_ResizeTeamArrays(ConfigReadyEvent evt)
    {
        var instance = UnityEngine.Object.FindFirstObjectByType<MainMenuManagerNetworked>();
        if (instance == null) return;

        var teamSize = evt.TeamSize;
        UnlimitedMagesPlugin.Log?.LogInfo($"Session config ready. Resizing MainMenuManagerNetworked arrays to size {teamSize}.");
        
        var team1Field = AccessTools.Field(typeof(MainMenuManagerNetworked), GameConstants.MainMenuManagerNetworked.Team1PlayersField);
        var team2Field = AccessTools.Field(typeof(MainMenuManagerNetworked), GameConstants.MainMenuManagerNetworked.Team2PlayersField);

        team1Field.SetValue(instance, new string[teamSize]);
        team2Field.SetValue(instance, new string[teamSize]);
    }
}
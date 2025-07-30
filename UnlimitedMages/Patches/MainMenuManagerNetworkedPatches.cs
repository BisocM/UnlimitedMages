using HarmonyLib;
using UnlimitedMages.Networking;
using UnlimitedMages.Utilities;

namespace UnlimitedMages.Patches;

/// <summary>
///     Contains Harmony patches for the <see cref="MainMenuManagerNetworked" /> class.
///     These patches resize server-side arrays that track player names on each team.
/// </summary>
[HarmonyPatch(typeof(MainMenuManagerNetworked))]
public static class MainMenuManagerNetworkedPatches
{
    /// <summary>
    ///     Postfixes the Start method to resize team arrays upon initialization.
    /// </summary>
    [HarmonyPatch(GameConstants.MainMenuManagerNetworked.StartMethod)]
    [HarmonyPostfix]
    public static void Start_Postfix(MainMenuManagerNetworked __instance)
    {
        ResizeTeamArrays(__instance);
    }

    /// <summary>
    ///     Postfixes the ResetLocalTeam method to ensure team arrays are resized when team data is reset.
    /// </summary>
    [HarmonyPatch(GameConstants.MainMenuManagerNetworked.ResetLocalTeamMethod)]
    [HarmonyPostfix]
    public static void ResetLocalTeam_Postfix(MainMenuManagerNetworked __instance)
    {
        ResizeTeamArrays(__instance);
    }

    /// <summary>
    ///     Resizes the internal string arrays used to store player names for each team.
    ///     This is crucial for the server's logic when assigning players to teams.
    /// </summary>
    /// <param name="instance">The instance of MainMenuManagerNetworked to patch.</param>
    private static void ResizeTeamArrays(MainMenuManagerNetworked instance)
    {
        if (NetworkedConfigManager.Instance == null) return;
        var teamSize = NetworkedConfigManager.Instance.TeamSize;

        var team1Field = AccessTools.Field(typeof(MainMenuManagerNetworked), GameConstants.MainMenuManagerNetworked.Team1PlayersField);
        var team2Field = AccessTools.Field(typeof(MainMenuManagerNetworked), GameConstants.MainMenuManagerNetworked.Team2PlayersField);

        // Set the fields to new string arrays with the configured size.
        team1Field.SetValue(instance, new string[teamSize]);
        team2Field.SetValue(instance, new string[teamSize]);
    }
}
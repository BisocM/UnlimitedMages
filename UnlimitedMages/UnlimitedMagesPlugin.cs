using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnlimitedMages.Utilities;

namespace UnlimitedMages
{
    /// <summary>
    /// Main plugin class for the Unlimited Mages mod. Initializes configuration and applies Harmony patches.
    /// </summary>
   
    public class UnlimitedMagesPlugin : BaseUnityPlugin
    {
        /// <summary>
        /// Internal logger instance for the plugin.
        /// </summary>
        internal static ManualLogSource? Log;

        /// <summary>
        /// Configuration entry for the maximum number of players allowed per team.
        /// This value is accessed by various patches to dynamically adjust game logic and UI.
        /// </summary>
        public static ConfigEntry<int>? TeamSizeConfig;

        /// <summary>
        /// BepInEx entry point. Called once upon plugin loading.
        /// </summary>
        private void Awake()
        {
            Log = Logger;

            TeamSizeConfig = Config.Bind(
                "General",
                "TeamSize",
                5,
                new ConfigDescription(
                    "The maximum number of players allowed per team.",
                    new AcceptableValueRange<int>(GameConstants.Game.OriginalTeamSize, 16) // Enforce a reasonable range.
                )
            );

            // Ensure the configured value is not below the original game's team size to prevent logic errors.
            if (TeamSizeConfig.Value < GameConstants.Game.OriginalTeamSize)
            {
                Log.LogWarning($"Configured team size ({TeamSizeConfig.Value}) is less than the original ({GameConstants.Game.OriginalTeamSize}). Clamping to {GameConstants.Game.OriginalTeamSize} to ensure stability.");
                TeamSizeConfig.Value = GameConstants.Game.OriginalTeamSize;
            }

            // Apply all Harmony patches defined within this assembly.
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

            Log.LogInfo("Unlimited Mages Mod has been loaded and patched successfully!");
            Log.LogInfo($"Current team size set to: {TeamSizeConfig.Value}");
        }
    }
}
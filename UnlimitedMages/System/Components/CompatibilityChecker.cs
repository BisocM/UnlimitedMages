using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using UnityEngine;
using UnlimitedMages.Components;
using UnlimitedMages.System.Attributes;
using UnlimitedMages.UI;
using UnlimitedMages.UI.Popup;

namespace UnlimitedMages.System.Components;

/// <summary>
///     A mod component that checks if the current game version is compatible with the mod.
///     It reads the <see cref="GameVersionCompatibilityAttribute" /> from the plugin's main class.
/// </summary>
internal sealed class CompatibilityChecker : MonoBehaviour, IModComponent
{
    private static bool IsVerified { get; set; }

    public void Initialize(ManualLogSource log)
    {
        if (IsVerified) return;

        log.LogInfo("[CompatibilityChecker] Checking game version compatibility...");

        var assembly = Assembly.GetExecutingAssembly();
        var pluginType = assembly.GetTypes()
            .FirstOrDefault(t => typeof(UnlimitedMagesPlugin) == t);

        if (pluginType == null)
        {
            log.LogWarning("[CompatibilityChecker] Could not find the BaseUnityPlugin class. Skipping check.");
            IsVerified = true;
            return;
        }

        // Find the compatibility and plugin info attributes on the main plugin class.
        var attribute = pluginType.GetCustomAttribute<GameVersionCompatibilityAttribute>();
        var pluginInfo = pluginType.GetCustomAttribute<BepInPlugin>();

        if (attribute == null)
        {
            log.LogInfo("[CompatibilityChecker] No GameVersionCompatibilityAttribute found. Assuming compatibility.");
            IsVerified = true;
            return;
        }

        PerformCheck(log, attribute, pluginInfo.Name);
    }

    /// <summary>
    ///     Performs the actual version check and logs a warning or shows a popup if an incompatibility is detected.
    /// </summary>
    private void PerformCheck(ManualLogSource log, GameVersionCompatibilityAttribute attribute, string modName)
    {
        var currentGameVersion = Application.version;
        var isCompatible = attribute.CompatibleVersions.Contains(currentGameVersion);

        if (!isCompatible)
        {
            log.LogWarning("--- GAME VERSION INCOMPATIBILITY DETECTED ---");
            log.LogWarning($"Mod: {modName}");
            log.LogWarning($"Current Game Version: {currentGameVersion}");
            log.LogWarning($"Compatible Versions: {string.Join(", ", attribute.CompatibleVersions)}");
            log.LogWarning("This mod may cause crashes or unexpected behavior. Proceed with caution.");
            log.LogWarning("---------------------------------------------");
            ShowIncompatibilityAlert(modName, currentGameVersion, attribute.CompatibleVersions);
        }
        else
        {
            log.LogInfo($"[CompatibilityChecker] Game version check passed. Current: {currentGameVersion}");
        }

        IsVerified = true;
    }

    /// <summary>
    ///     Displays an in-game popup to the user, alerting them of the version mismatch.
    /// </summary>
    private void ShowIncompatibilityAlert(string modName, string currentVersion, string[] compatibleVersions)
    {
        var title = "Mod Compatibility Warning";
        var message = $"The mod <b>{modName}</b> has not been marked as compatible with your current game version.\n\n" +
                      $"<b>Your Version:</b>\n<size=20>{currentVersion}</size>\n\n" +
                      $"<b>Compatible Versions:</b>\n<size=20>{string.Join(", ", compatibleVersions)}</size>\n\n" +
                      $"<color=yellow>Using an incompatible version may cause issues. Proceed with caution.</color>\n" +
                      $"<size=12><color=grey>If you observe any bugs, please report them in the Modding Discord or on GitHub!</color></size>";

        ModUIManager.Instance?.ShowPopup(title, message, _ => { }, new PopupButtonData(PopupButton.Warning, "OK"));
    }
}
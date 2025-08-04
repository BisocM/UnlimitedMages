using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnlimitedMages.Components;
using UnlimitedMages.System.Attributes;
using UnlimitedMages.System.Events;
using UnlimitedMages.System.Events.Types;

namespace UnlimitedMages;

/// <summary>
///     The main entry point for the Unlimited Mages BepInEx plugin.
///     This class is responsible for initializing the logger, applying Harmony patches,
///     and setting up the event-driven system for component injection.
/// </summary>
[BepInPlugin(ModGuid, ModName, ModVersion)]
[GameVersionCompatibility("0.7.4", "0.7.6")]
public partial class UnlimitedMagesPlugin : BaseUnityPlugin
{
    /// <summary>
    ///     The static logger instance for the mod.
    /// </summary>
    internal static ManualLogSource? Log;

    /// <summary>
    ///     The entry point method called by BepInEx when the plugin is loaded.
    /// </summary>
    private void Awake()
    {
        Log = BepInEx.Logging.Logger.CreateLogSource("UnlimitedMages");

        // Subscribe to the BootstrapReadyEvent to know when it's safe to inject our components.
        EventBus.Subscribe<BootstrapReadyEvent>(OnBootstrapReady);

        // Apply all Harmony patches defined in this assembly.
        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

        Log.LogInfo("Unlimited Mages Mod has been loaded and patched successfully!");
    }

    private void OnDestroy()
    {
        // Clean up event subscriptions when the plugin is unloaded.
        EventBus.Unsubscribe<BootstrapReadyEvent>(OnBootstrapReady);
    }

    /// <summary>
    ///     Event handler for when the BootstrapManager is ready. This triggers the injection of all mod components.
    /// </summary>
    private void OnBootstrapReady(BootstrapReadyEvent evt)
    {
        Log?.LogInfo("BootstrapReady event received. Initializing mod systems...");
        ComponentActivator.Inject(Log!);
    }
}
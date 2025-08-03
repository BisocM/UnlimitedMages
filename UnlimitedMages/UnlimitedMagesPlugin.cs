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
///     Main plugin class for the Unlimited Mages mod. Initializes configuration and applies Harmony patches.
/// </summary>
[BepInPlugin(ModGuid, ModName, ModVersion)]
[GameVersionCompatibility("0.7.4", "0.7.6")]
public partial class UnlimitedMagesPlugin : BaseUnityPlugin
{
    /// <summary>
    ///     Internal logger instance for the plugin.
    /// </summary>
    internal static ManualLogSource? Log;

    /// <summary>
    ///     BepInEx entry point. Called once upon plugin loading.
    /// </summary>
    private void Awake()
    {
        Log = BepInEx.Logging.Logger.CreateLogSource("UnlimitedMages");

        EventBus.Subscribe<BootstrapReadyEvent>(OnBootstrapReady);

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

        Log.LogInfo("Unlimited Mages Mod has been loaded and patched successfully!");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        EventBus.Unsubscribe<BootstrapReadyEvent>(OnBootstrapReady);
    }

    /// <summary>
    ///     This method is called by the OnBootstrapReady event when the game's core manager is ready.
    ///     It triggers the injection of all custom mod components.
    /// </summary>
    private void OnBootstrapReady(BootstrapReadyEvent evt)
    {
        Log?.LogInfo("BootstrapReady event received. Initializing mod systems...");
        ComponentActivator.Inject(Log!);
    }
}
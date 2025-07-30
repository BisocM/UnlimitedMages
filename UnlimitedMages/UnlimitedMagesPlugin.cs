using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnlimitedMages.Components;
using UnlimitedMages.System;

namespace UnlimitedMages;

/// <summary>
///     Main plugin class for the Unlimited Mages mod. Initializes configuration and applies Harmony patches.
/// </summary>
[BepInPlugin("com.bisocm.unlimited_mages", "Unlimited Mages", ModVersion)]
public class UnlimitedMagesPlugin : BaseUnityPlugin
{
    /// <summary>
    ///     Publicly accessible mod version.
    /// </summary>
    public const string ModVersion = "1.1.1";

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

        ModLifecycleEvents.OnBootstrapReady += InitializeModSystems;

        Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());

        Log.LogInfo("Unlimited Mages Mod has been loaded and patched successfully!");
    }

    private void OnDestroy()
    {
        // Unsubscribe from events to prevent memory leaks
        ModLifecycleEvents.OnBootstrapReady -= InitializeModSystems;
    }

    /// <summary>
    ///     This method is called by the OnBootstrapReady event when the game's core manager is ready.
    ///     It triggers the injection of all custom mod components.
    /// </summary>
    private void InitializeModSystems()
    {
        Log?.LogInfo("OnBootstrapReady event received. Initializing mod systems...");
        ComponentActivator.Inject(Log!);
    }
}
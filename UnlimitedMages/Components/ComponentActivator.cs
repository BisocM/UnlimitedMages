using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace UnlimitedMages.Components;

/// <summary>
///     Handles the dynamic injection of mod components into the game's scene.
///     This class is responsible for finding and initializing all custom systems that implement <see cref="IModComponent" />.
/// </summary>
internal static class ComponentActivator
{
    /// <summary>
    ///     Scans the executing assembly for all types that implement <see cref="IModComponent" />
    ///     and adds them as <see cref="MonoBehaviour" /> components to the game's <see cref="BootstrapManager" /> instance.
    /// </summary>
    /// <param name="log">The logger instance to use for recording injection progress and status.</param>
    public static void Inject(ManualLogSource log)
    {
        log.LogInfo("--- Injecting Mod Components onto BootstrapManager ---");
        var bmGo = BootstrapManager.instance.gameObject;

        // Find all concrete classes that implement the IModComponent interface.
        var componentTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IModComponent).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            .ToList();

        log.LogInfo($"Found {componentTypes.Count} component(s) to inject.");
        foreach (var type in componentTypes)
        {
            // Prevent duplicate injections.
            if (bmGo.GetComponent(type) != null) continue;

            // Add the component to the BootstrapManager's GameObject and initialize it.
            var addedComponent = bmGo.AddComponent(type) as IModComponent;
            addedComponent?.Initialize(log);
            log.LogInfo($"Successfully injected component: {type.Name}");
        }
    }
}
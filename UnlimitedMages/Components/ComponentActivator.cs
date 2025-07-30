using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using UnityEngine;

namespace UnlimitedMages.Components;

/// <summary>
///     A static utility class responsible for discovering and injecting custom mod components
///     into a persistent GameObject at runtime.
/// </summary>
public static class ComponentActivator
{
    /// <summary>
    ///     Scans the executing assembly for all concrete classes that implement <see cref="IModComponent" />,
    ///     adds them as a <see cref="Component" /> to the BootstrapManager's GameObject, and initializes them.
    /// </summary>
    /// <param name="log">The logger instance to use for reporting progress and errors.</param>
    public static void Inject(ManualLogSource log)
    {
        log.LogInfo("--- Injecting Mod Components onto BootstrapManager ---");
        var bmGo = BootstrapManager.instance.gameObject;

        var componentTypes = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => typeof(IModComponent).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract)
            .ToList();

        log.LogInfo($"Found {componentTypes.Count} component(s) to inject.");
        foreach (var type in componentTypes)
        {
            // Ensure the component isn't already attached to prevent duplicates.
            if (bmGo.GetComponent(type) != null) continue;

            var addedComponent = bmGo.AddComponent(type) as IModComponent;
            addedComponent?.Initialize(log);
            log.LogInfo($"Successfully injected component: {type.Name}");
        }
    }
}
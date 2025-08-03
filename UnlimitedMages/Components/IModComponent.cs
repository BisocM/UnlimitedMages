using BepInEx.Logging;

namespace UnlimitedMages.Components;

/// <summary>
///     Defines the contract for a component that can be automatically
///     discovered and injected into the game's persistent manager.
/// </summary>
internal interface IModComponent
{
    /// <summary>
    ///     Initializes the component with necessary dependencies.
    /// </summary>
    void Initialize(ManualLogSource log);
}
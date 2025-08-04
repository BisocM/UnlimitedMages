using BepInEx.Logging;

namespace UnlimitedMages.Components;

/// <summary>
///     Defines the contract for a modular component of the mod.
///     Components implementing this interface will be automatically discovered and initialized.
/// </summary>
internal interface IModComponent
{
    /// <summary>
    ///     Initializes the component, setting up its state and any required hooks.
    /// </summary>
    /// <param name="log">The logger instance provided for this component.</param>
    void Initialize(ManualLogSource log);
}
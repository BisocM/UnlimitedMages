using System;

namespace UnlimitedMages.System.Attributes;

/// <summary>
///     An attribute to specify which versions of the game this mod is compatible with.
///     Used by the <see cref="Components.CompatibilityChecker" /> to warn users of potential issues.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal class GameVersionCompatibilityAttribute(params string[] compatibleVersions) : Attribute
{
    /// <summary>
    ///     Gets the array of game version strings that the mod is marked as compatible with.
    /// </summary>
    public string[] CompatibleVersions { get; } = compatibleVersions;
}
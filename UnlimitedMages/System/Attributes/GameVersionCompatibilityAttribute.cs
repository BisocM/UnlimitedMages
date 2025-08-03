using System;

namespace UnlimitedMages.System.Attributes;

/// <summary>
///     Specifies the compatible game versions for the BepInEx plugin.
///     This attribute is intended to be used on classes inheriting from BaseUnityPlugin.
///     <param name="compatibleVersions">A list of compatible game version strings (e.g., "0.7.6").</param>
/// </summary>
// AttributeUsage is restricted to Class targets.
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
internal class GameVersionCompatibilityAttribute(params string[] compatibleVersions) : Attribute
{
    public string[] CompatibleVersions { get; } = compatibleVersions;
}
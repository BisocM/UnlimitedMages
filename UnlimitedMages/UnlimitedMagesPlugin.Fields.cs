using BepInEx;

namespace UnlimitedMages;

//TODO: Temporarily pulled out due to compatibility issues. Will need to investigate.
//[BepInDependency("com.magearena.modsync")] // Hard dependency on ModSync - requires all players in the lobby to have it installed.
public partial class UnlimitedMagesPlugin
{
    #region Cross-Mod Compatibility

    /// <summary>
    ///     Compatibility string for the ModSync plugin.
    ///     "all" indicates that both the client and the host need the mod installed.
    /// </summary>
    public static string modsync = "all";

    #endregion

    #region Mod-Related Strings

    /// <summary>
    ///     Publicly accessible mod version.
    /// </summary>
    public const string ModVersion = "2.0.0";

    /// <summary>
    ///     The mod's display name.
    /// </summary>
    public const string ModName = "Unlimited Mages";

    /// <summary>
    ///     The mod's GUID.
    /// </summary>
    public const string ModGuid = "com.bisocm.unlimited_mages";

    #endregion
}
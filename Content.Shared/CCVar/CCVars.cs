using Content.Shared.Administration;
using Content.Shared.CCVar.CVarAccess;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

/// <summary>
/// Contains all the CVars used by content.
/// </summary>
/// <remarks>
/// NOTICE FOR FORKS: Put your own CVars in a separate file with a different [CVarDefs] attribute. RT will automatically pick up on it.
/// </remarks>
[CVarDefs]
public sealed partial class CCVars : CVars
{
    // Only debug stuff lives here.

#if DEBUG
    [CVarControl(AdminFlags.Debug)]
    public static readonly CVarDef<string> DebugTestCVar =
        CVarDef.Create("debug.test_cvar", "default", CVar.SERVER);

    [CVarControl(AdminFlags.Debug)]
    public static readonly CVarDef<float> DebugTestCVar2 =
        CVarDef.Create("debug.test_cvar2", 123.42069f, CVar.SERVER);
#endif

    /// <summary>
    /// A simple toggle to test <c>OptionsVisualizerComponent</c>.
    /// </summary>
    public static readonly CVarDef<bool> DebugOptionVisualizerTest =
        CVarDef.Create("debug.option_visualizer_test", false, CVar.CLIENTONLY);

        /// DELTA-V CCVARS
        /*
         * Glimmer
         */

        /// <summary>
        ///    Whether glimmer is enabled.
        /// </summary>
        public static readonly CVarDef<bool> GlimmerEnabled =
            CVarDef.Create("glimmer.enabled", true, CVar.REPLICATED);

        /// <summary>
        ///     Passive glimmer drain per second.
        ///     Note that this is randomized and this is an average value.
        /// </summary>
        public static readonly CVarDef<float> GlimmerLostPerSecond =
            CVarDef.Create("glimmer.passive_drain_per_second", 0.1f, CVar.SERVERONLY);

        /// <summary>
        ///     Whether random rolls for psionics are allowed.
        ///     Guaranteed psionics will still go through.
        /// </summary>
        public static readonly CVarDef<bool> PsionicRollsEnabled =
            CVarDef.Create("psionics.rolls_enabled", true, CVar.SERVERONLY);

        /// <summary>
        ///     Whether height & width sliders adjust a character's Fixture Component
        /// </summary>
        public static readonly CVarDef<bool> HeightAdjustModifiesHitbox =
            CVarDef.Create("heightadjust.modifies_hitbox", true, CVar.SERVERONLY);

        /// <summary>
        ///     Whether height & width sliders adjust a player's max view distance
        /// </summary>
        public static readonly CVarDef<bool> HeightAdjustModifiesZoom =
            CVarDef.Create("heightadjust.modifies_zoom", true, CVar.SERVERONLY);

        /// <summary>
        ///     Enables station goals
        /// </summary>
        public static readonly CVarDef<bool> StationGoalsEnabled =
            CVarDef.Create("game.station_goals", true, CVar.SERVERONLY);

        /// <summary>
        ///     Chance for a station goal to be sent
        /// </summary>
        public static readonly CVarDef<float> StationGoalsChance =
            CVarDef.Create("game.station_goals_chance", 0.1f, CVar.SERVERONLY);

        /// <summary>
        /// How many characters the consent text can be.
        /// </summary>
        public static readonly CVarDef<int> ConsentFreetextMaxLength =
            CVarDef.Create("consent.freetext_max_length", 1000, CVar.REPLICATED | CVar.SERVER);
    }
}

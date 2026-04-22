using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<int> NPCMaxUpdates =
        CVarDef.Create("npc.max_updates", 512);

    public static readonly CVarDef<bool> NPCEnabled = CVarDef.Create("npc.enabled", true);

    /// <summary>
    ///     Should NPCs pathfind when steering. For debug purposes.
    /// </summary>
    public static readonly CVarDef<bool> NPCPathfinding = CVarDef.Create("npc.pathfinding", true);

    /// <summary>
    ///     If true, shared-path reuse is restricted to NPCs actively chasing a combat target.
    /// </summary>
    public static readonly CVarDef<bool> NPCPathfindingCombatOnly =
        CVarDef.Create("npc.pathfinding_combat_only", true);

    /// <summary>
    ///     If true, NPC steering can reuse nearby NPC paths when destinations are similar.
    /// </summary>
    public static readonly CVarDef<bool> NPCPathShareEnabled =
        CVarDef.Create("npc.path_share_enabled", true);

    /// <summary>
    ///     Max distance between NPCs to allow path reuse.
    /// </summary>
    public static readonly CVarDef<float> NPCPathShareRadius =
        CVarDef.Create("npc.path_share_radius", 8f);

    /// <summary>
    ///     Max distance from chase target to allow joining a shared chase-path group.
    /// </summary>
    public static readonly CVarDef<float> NPCPathShareActivationRange =
        CVarDef.Create("npc.path_share_activation_range", 20f);

    /// <summary>
    ///     Max distance between destinations to allow path reuse.
    /// </summary>
    public static readonly CVarDef<float> NPCPathShareTargetTolerance =
        CVarDef.Create("npc.path_share_target_tolerance", 3f);

    /// <summary>
    ///     Chance that an NPC temporarily breaks from shared-path chaining and computes its own path.
    /// </summary>
    public static readonly CVarDef<float> NPCPathShareBreakawayChance =
        CVarDef.Create("npc.path_share_breakaway_chance", 0.33f);

    /// <summary>
    ///     How long (seconds) a breakaway NPC keeps using independent pathfinding before rejoining chain reuse.
    /// </summary>
    public static readonly CVarDef<float> NPCPathShareBreakawayDuration =
        CVarDef.Create("npc.path_share_breakaway_duration", 2f);

    /// <summary>
    ///     If direct target distance is less than this fraction of shared-entry distance,
    ///     prefer independent pathing over shared chaining.
    /// </summary>
    public static readonly CVarDef<float> NPCPathShareDirectOverrideRatio =
        CVarDef.Create("npc.path_share_direct_override_ratio", 0.70f);

    /// <summary>
    ///     If true, NPCs that are not actively in combat can still reuse shared paths.
    /// </summary>
    public static readonly CVarDef<bool> NPCPathShareNonCombatEnabled =
        CVarDef.Create("npc.path_share_noncombat_enabled", false);

    /// <summary>
    ///     If true, non-combat shared-path followers apply small path variation to avoid rigid clumping.
    /// </summary>
    public static readonly CVarDef<bool> NPCPathShareNonCombatDynamic =
        CVarDef.Create("npc.path_share_noncombat_dynamic", true);

    /// <summary>
    ///     Max number of initial shared nodes a non-combat follower can skip for formation variation.
    /// </summary>
    public static readonly CVarDef<int> NPCPathShareNonCombatMaxSkip =
        CVarDef.Create("npc.path_share_noncombat_max_skip", 1);

    /// <summary>
    ///     Chance that a non-combat follower flips a loop-like shared path direction.
    /// </summary>
    public static readonly CVarDef<float> NPCPathShareNonCombatFlipChance =
        CVarDef.Create("npc.path_share_noncombat_flip_chance", 0.04f);

    /// <summary>
    ///     Endpoint distance tolerance (in tiles) used to detect loop-like shared paths eligible for flip.
    /// </summary>
    public static readonly CVarDef<float> NPCPathShareLoopFlipEndpointTolerance =
        CVarDef.Create("npc.path_share_loop_flip_endpoint_tolerance", 1.00f);
    /// <summary>
    ///     #Misfits Add: Override for juke cooldown timing. -1 uses default behavior, >0 overrides all juke cooldowns.
    /// </summary>
    public static readonly CVarDef<float> NPCJukeCooldownOverride = CVarDef.Create("npc.juke_cooldown_override", -1f);

    /// <summary>
    /// How often (seconds) the proximity NPC system scans for nearby players.
    /// Higher values are cheaper but increase the delay before an NPC wakes.
    /// </summary>
    public static readonly CVarDef<float> ProximityNPCCheckInterval =
        CVarDef.Create("misfits.proximity_npc_check_interval", 5f, CVar.SERVER | CVar.SERVERONLY);

}

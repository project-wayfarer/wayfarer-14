using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /*
     * Roleplay Leveling
     */

    /// <summary>
    /// Amount of XP awarded for sending a chat message
    /// </summary>
    public static readonly CVarDef<int> RoleplayXpChat =
        CVarDef.Create("roleplay.xp_chat", 1, CVar.SERVERONLY);

    /// <summary>
    /// Amount of XP awarded for performing an emote
    /// </summary>
    public static readonly CVarDef<int> RoleplayXpEmote =
        CVarDef.Create("roleplay.xp_emote", 5, CVar.SERVERONLY);

    /// <summary>
    /// Amount of XP awarded for receiving a commend
    /// </summary>
    public static readonly CVarDef<int> RoleplayXpCommend =
        CVarDef.Create("roleplay.xp_commend", 250, CVar.SERVERONLY);

    /// <summary>
    /// Number of commends players start with when joining a round
    /// </summary>
    public static readonly CVarDef<int> RoleplayCommendStart =
        CVarDef.Create("roleplay.commend_start", 1, CVar.SERVERONLY);

    /// <summary>
    /// Maximum number of commends a player can have available at a given time
    /// </summary>
    public static readonly CVarDef<int> RoleplayCommendMax =
        CVarDef.Create("roleplay.commend_max", 3, CVar.SERVERONLY);

    /// <summary>
    /// Hours of round playtime required to earn each additional commend (after the first free one)
    /// </summary>
    public static readonly CVarDef<float> RoleplayCommendHours =
        CVarDef.Create("roleplay.commend_hours", 2.0f, CVar.SERVERONLY);
}

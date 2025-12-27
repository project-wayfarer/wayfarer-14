using Robust.Shared.Serialization;

namespace Content.Server._Coyote;

/// <summary>
/// Enum of possible roleplay actions.
/// </summary>
[Serializable]
public enum RoleplayActs : byte
{
    /// <summary>
    /// The player has said a thing, normally.
    /// </summary>
    Speaking,

    /// <summary>
    /// The player has whispered something.
    /// </summary>
    Whispering,

    /// <summary>
    /// The player has done a non-quick emote.
    /// </summary>
    Emoting,

    /// <summary>
    /// The player has done a quick emote. (*gekkers)
    /// </summary>
    QuickEmoting,

    /// <summary>
    /// The player has done a sexy subtle emote. (*removes pants)
    /// </summary>
    Subtling,

    /// <summary>
    /// The player has used the radio.
    /// </summary>
    Radio,

    /// <summary>
    /// Its an emote OR a quick emote, so more dickery is needed to figure it
    /// out.
    /// </summary>
    EmotingOrQuickEmoting,

    /// <summary>
    /// null but not really
    /// </summary>
    None,
}

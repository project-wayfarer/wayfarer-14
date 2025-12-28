using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.InteractionVerbs;

/// <summary>
///     Contains the localization strings and settings for the popups shown during an interaction verb.
/// </summary>
/// <remarks>
///     Interaction popups are localized using the following pattern:
///     `interaction-{VerbID}-{Prefix.ToString().ToLower()}-{TargetSuffix}-popup`
///     Available variables in the localization strings:
///     - {$user} - The user performing the verb.
///     - {$target} - The target of the verb.
///     - {$used} - The item used for the verb (if any).
///     - {$selfTarget} - A boolean value that indicates whether the action is used on the user itself.
///     - {$hasUsed} - A boolean value that indicates whether the user is holding an item ($used is not null).
/// </remarks>
[Prototype("InteractionPopup"), Serializable]
public sealed partial class InteractionPopupPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    ///     Loc prefix for popups shown for the user performing the verb.
    /// </summary>
    [DataField("self")]
    public string SelfSuffix = "self";

    /// <summary>
    ///     Loc prefix for popups shown for the target of the verb. If set to null, defaults to <see cref="OthersSuffix"/>.
    /// </summary>
    [DataField("target")]
    public string? TargetSuffix = "target";

    /// <summary>
    ///     Loc prefix for popups shown for other people observing the verb. If null, no popup will be shown for others.
    /// </summary>
    [DataField("others")]
    public string? OthersSuffix = "others";

    public enum Prefix : byte
    {
        Success,
        Fail,
        Delayed
    }
}

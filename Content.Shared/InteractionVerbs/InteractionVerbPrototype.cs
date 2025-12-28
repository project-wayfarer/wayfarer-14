using Content.Shared.DoAfter;
using Content.Shared.InteractionVerbs.Events;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;
using Robust.Shared.Utility;

#pragma warning disable CS0618 // Type or member is obsolete

namespace Content.Shared.InteractionVerbs;

/// <summary>
///     Represents an action that can be performed on an entity.
/// </summary>
[Prototype("Interaction"), Serializable]
public sealed partial class InteractionVerbPrototype : IPrototype, IInheritingPrototype
{
    /// <inheritdoc />
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<InteractionVerbPrototype>))]
    public string[]? Parents { get; }

    /// <inheritdoc />
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; }

    [IdDataField]
    public string ID { get; } = default!;

    // Locale getters
    public string Name => Loc.TryGetString($"interaction-{ID}-name", out var loc) ? loc : ID;

    public string? Description => Loc.TryGetString($"interaction-{ID}-description" , out var loc) ? loc : null;

    /// <summary>
    ///     Sprite of the icon that the user sees on the verb button.
    /// </summary>
    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>
    ///     Specifies what effects are shown when this verb is performed successfully, or unsuccessfully.
    ///     Effects specified here are shown after the associated do-after has ended, if any.
    /// </summary>
    [DataField]
    public EffectSpecifier? EffectSuccess, EffectFailure;

    /// <summary>
    ///     Specifies what popups are shown when a do-after for this verb is started.
    ///     This is only ever used if <see cref="Delay"/> is set to a non-zero value.
    /// </summary>
    [DataField]
    public EffectSpecifier? EffectDelayed;

    /// <summary>
    ///     The requirement of this verb.
    /// </summary>
    [DataField]
    public InteractionRequirement? Requirement = null;

    /// <summary>
    ///     The action of this verb. It defines the conditions under which this verb is shown, as well as what the verb does.
    /// </summary>
    /// <remarks>Made server-only because many actions require authoritative access to the server.</remarks>
    [DataField(serverOnly: true)]
    public InteractionAction? Action = null;

    /// <summary>
    ///     If true, this action will be hidden if the <see cref="Requirement"/> does not pass its IsMet check. Otherwise it will be shown, but disabled.
    /// </summary>
    [DataField]
    public bool HideByRequirement = false;

    /// <summary>
    ///     If true, this action will be hidden if the <see cref="Action"/> does not pass its IsAllowed check. Otherwise it will be shown, but disabled.
    /// </summary>
    [DataField]
    public bool HideWhenInvalid = true;

    /// <summary>
    ///     The delay of the verb. Anything greater than zero constitutes a do-after.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.Zero;

    /// <summary>
    ///     Cooldown between uses of this verb. Applied per user or per user-target pair and before the do-after.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(0.5f);

    /// <summary>
    ///     If true, the cooldown of this verb will be applied regardless of the verb target,
    ///     i.e. a user won't be able to apply the same verb to any different entity until the cooldown ends.
    /// </summary>
    [DataField]
    public bool GlobalCooldown = false;

    [DataField]
    public RangeSpecifier Range = new();

    /// <summary>
    ///     Whether this interaction implies direct body contact (transfer of fibers, fingerprints, etc).
    /// </summary>
    [DataField("contactInteraction")]
    public bool DoContactInteraction = true;

    [DataField]
    public bool RequiresHands = false;

    /// <summary>
    ///     Whether this verb requires the user to be able to access the target normally (with their hands or otherwise).
    /// </summary>
    [DataField("requiresCanInteract")]
    public bool RequiresCanAccess = true;

    /// <summary>
    ///     If true, this verb can be invoked by the user on itself.
    /// </summary>
    [DataField]
    public bool AllowSelfInteract = false;

    /// <summary>
    ///     Priority of the verb. Verbs with higher priority will be shown first.
    /// </summary>
    [DataField]
    public int Priority = 0;

    /// <summary>
    ///     If true, this verb can be invoked on any entity that the action is allowed on, even if its components don't specify it.
    /// </summary>
    [DataField]
    public bool Global = false;

    /// <summary>
    ///     The category key for the verb. Can be used to specify custom categories like "interact-sfw", "interact-nsfw", "actions", etc.
    ///     If not specified, defaults to "interaction".
    /// </summary>
    [DataField]
    public string? CategoryKey = null;

    [DataDefinition, Serializable]
    public partial struct RangeSpecifier()
    {
        [DataField]
        public float Min = 0f, Max = float.PositiveInfinity;
    }

    [DataDefinition, Serializable]
    public partial class EffectSpecifier
    {
        [DataField]
        public EffectTargetSpecifier EffectTarget = EffectTargetSpecifier.TargetThenUser;

        /// <summary>
        ///     The interaction popup to show. If null, no popup will be shown.
        /// </summary>
        [DataField]
        public ProtoId<InteractionPopupPrototype>? Popup = null;

        /// <summary>
        ///     Sound played when the effect is shown. If null, no sound will be played.
        /// </summary>
        [DataField]
        public SoundSpecifier? Sound;

        /// <summary>
        ///     If true, the sound will be perceived by everyone in the PVS of the popup.
        ///     Otherwise, it will be perceived only by the target and the user.
        /// </summary>
        [DataField]
        public bool SoundPerceivedByOthers = true;

        /// <summary>
        ///     If true, then the popup will be obvious if the target is a non-player entity.
        /// </summary>
        [DataField]
        public bool ObviousIfTargetIsNonPlayer = false;

        [DataField]
        public AudioParams SoundParams = new AudioParams()
        {
            Variation = 0.1f
        };
    }

    [Serializable, Flags]
    public enum EffectTargetSpecifier
    {
        /// <summary>
        ///     Popup will be shown above the person executing the verb.
        /// </summary>
        User,
        /// <summary>
        ///     Popup will be shown above the target of the verb.
        /// </summary>
        Target,
        /// <summary>
        ///     The user will see the popup shown above itself, others will see the popup above the target.
        /// </summary>
        UserThenTarget,
        /// <summary>
        ///     The target will see the popup shown above itself, others will see the popup above the user.
        /// </summary>
        TargetThenUser
    }
}

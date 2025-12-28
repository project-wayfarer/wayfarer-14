using Robust.Shared.Serialization;

namespace Content.Shared.InteractionVerbs;

public sealed partial class InteractionArgs
{
    public EntityUid User, Target;
    public EntityUid? Used;
    public bool CanAccess, CanInteract, HasHands;

    /// <summary>
    ///     A float value between 0 and positive infinity that indicates how much stronger the user
    ///     is compared to the target in terms of contests allowed for this verb. 1.0 means no advantage or disadvantage.
    /// </summary>
    /// <remarks>Can be null, which means it's not calculated yet. That can happen before the user attempts to perform the verb.</remarks>
    public float? ContestAdvantage;

    /// <summary>
    ///     A dictionary for actions and requirements to store data between different execution stages.
    ///     For instance, an action can cache some data in its CanPerform check and later use it in Perform.
    /// </summary>
    /// <remarks>
    ///     Non-action classes are highly not recommended to write anything to this dictionary - it can easily lead to errors.
    /// </remarks>
    public Dictionary<string, object> CustomData = new();

    public InteractionArgs(EntityUid user, EntityUid target, EntityUid? used, bool canAccess, bool canInteract, bool hasHands, float? contestAdvantage)
    {
        User = user;
        Target = target;
        Used = used;
        CanAccess = canAccess;
        CanInteract = canInteract;
        HasHands = hasHands;
        ContestAdvantage = contestAdvantage;
    }

    public InteractionArgs(InteractionArgs other) : this(other.User, other.Target, other.Used, other.CanAccess, other.CanInteract, other.HasHands, other.ContestAdvantage) {}
}

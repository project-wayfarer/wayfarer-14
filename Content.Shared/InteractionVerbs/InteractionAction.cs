using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Shared.InteractionVerbs;

/// <summary>
///     Represents an action performed when a verb is used successfully.
/// </summary>
[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public abstract partial class InteractionAction
{
    /// <summary>
    ///     Invoked when the user wants to get the list of verbs that can be performed on the target, after all verb-specific checks have passed.
    ///     If this method returns false, it will not be shown to the user.
    /// </summary>
    public virtual bool IsAllowed(
        InteractionArgs args,
        InteractionVerbPrototype proto,
        VerbDependencies deps
    ) => true;

    /// <summary>
    ///     Checks whether this verb can be performed at the current moment.
    ///     If the verb has a do-after, this will be called both before and after the do-after.
    /// </summary>
    public abstract bool CanPerform(
        InteractionArgs args,
        InteractionVerbPrototype proto,
        bool beforeDelay,
        VerbDependencies deps
    );

    /// <summary>
    ///     Performs the action and returns whether it was successful.
    /// </summary>
    public abstract bool Perform(
        InteractionArgs args,
        InteractionVerbPrototype proto,
        VerbDependencies deps
    );

    public sealed partial class VerbDependencies(
        IEntityManager entMan,
        IPrototypeManager protoMan,
        IRobustRandom random,
        IGameTiming gameTiming,
        ISerializationManager serializationManager)
    {
        public readonly IEntityManager EntityManager = entMan;
        public readonly IPrototypeManager PrototypeManager = protoMan;
        public readonly IRobustRandom Random = random;
        public readonly IGameTiming Timing = gameTiming;
        public readonly ISerializationManager SerializationManager = serializationManager;
    }
}

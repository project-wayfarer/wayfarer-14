using Content.Shared.Verbs;
using Robust.Shared.Serialization;

namespace Content.Shared.InteractionVerbs;

/// <summary>
///     Defines a requirement for an <see cref="InteractionVerb"/>.
///     If a verb does not meet the requirement, it will be hidden or disabled in the verb menu.
/// </summary>
[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public abstract partial class InteractionRequirement
{
    public abstract bool IsMet(InteractionArgs args, InteractionVerbPrototype proto, InteractionAction.VerbDependencies deps);
}

/// <inheritdoc cref="InteractionRequirement"/>
[Serializable, NetSerializable]
public abstract partial class InvertableInteractionRequirement : InteractionRequirement
{
    [DataField] public bool Inverted = false;
}

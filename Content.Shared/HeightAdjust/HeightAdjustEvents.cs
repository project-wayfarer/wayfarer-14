using System.Collections.Generic;

namespace Content.Shared.HeightAdjust;

/// <summary>
/// Event raised to request recalculation of an entity's size.
/// This should trigger collection of all active size modifiers and apply them.
/// </summary>
[ByRefEvent]
public record struct RequestSizeRecalcEvent;

/// <summary>
/// Event raised to collect all active size modifiers for an entity.
/// Systems that modify entity size should subscribe to this event and add their modifiers.
/// </summary>
[ByRefEvent]
public record struct GetSizeModifierEvent(EntityUid Target)
{
    public readonly EntityUid Target = Target;
    public List<SizeModifier> Modifiers = new();
}

/// <summary>
/// Represents a single size modification with a priority.
/// Lower priority modifiers are applied first.
/// </summary>
public readonly record struct SizeModifier(float Scale, int Priority = 0);

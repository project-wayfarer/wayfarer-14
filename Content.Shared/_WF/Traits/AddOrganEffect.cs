using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Traits.Effects;

/// <summary>
/// Effect that adds a specific organ prototype to the player's body in a named organ slot.
/// </summary>
public sealed partial class AddOrganEffect : BaseTraitEffect
{
    /// <summary>
    /// The organ prototype to add to the entity.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Organ = string.Empty;

    /// <summary>
    /// The organ slot on the root body part to insert into.
    /// Defaults to the stomach slot for body organ traits.
    /// </summary>
    [DataField]
    public string Slot = "stomach";

    public override void Apply(TraitEffectContext ctx)
    {
        if (string.IsNullOrEmpty(Organ))
            return;

        var bodySystem = ctx.EntMan.System<SharedBodySystem>();
        var rootPart = bodySystem.GetRootPartOrNull(ctx.Player);

        if (rootPart is null)
            return;

        var organ = ctx.EntMan.SpawnEntity(Organ, ctx.Transform.Coordinates);

        if (!bodySystem.CanInsertOrgan(rootPart.Value.Entity, Slot)
            && !bodySystem.TryCreateOrganSlot(rootPart.Value.Entity, Slot, out _))
        {
            ctx.EntMan.DeleteEntity(organ);
            return;
        }

        if (!bodySystem.InsertOrgan(rootPart.Value.Entity, organ, Slot))
            ctx.EntMan.DeleteEntity(organ);
    }
}

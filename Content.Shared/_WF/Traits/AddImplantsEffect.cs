using Content.Shared.Implants;
using Robust.Shared.Prototypes;

namespace Content.Shared._DV.Traits.Effects;

/// <summary>
/// Effect that adds implants to the player entity.
/// </summary>
public sealed partial class AddImplantsEffect : BaseTraitEffect
{
    [DataField]
    public HashSet<EntProtoId> Implants = new();

    public override void Apply(TraitEffectContext ctx)
    {
        ctx.EntMan.System<SharedSubdermalImplantSystem>().AddImplants(ctx.Player, Implants);
    }
}
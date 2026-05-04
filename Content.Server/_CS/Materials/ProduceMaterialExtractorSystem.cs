using System.Linq;
using Content.Server.Botany.Components;
using Content.Server.Materials.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Storage.Components;
using Content.Shared.Popups;
using Robust.Server.Audio;

using Content.Shared.Storage.EntitySystems; // Coyote: Biogen magnet

namespace Content.Server.Materials;

public sealed partial class ProduceMaterialExtractorSystem
{
    private void OnFeedProduce(Entity<ProduceMaterialExtractorComponent> ent, ref FeedProduceEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = EatTheProduce(ent, args.Used);
    }
    private bool EatTheProduce(Entity<ProduceMaterialExtractorComponent> ent, EntityUid used)
    {
        if (!this.IsPowered(ent, EntityManager))
            return false;

        if (!TryComp<ProduceComponent>(used, out var produce))
            return false;

        if (!_solutionContainer.TryGetSolution(used, produce.SolutionName, out var solution))
            return false;

        bool success = true;
        // Can produce even have fractional amounts? Does it matter if they do?
        // Questions man was never meant to answer.
        var matAmount = solution.Value.Comp.Solution.Contents
            .Where(r => ent.Comp.ExtractionReagents.Contains(r.Reagent.Prototype))
            .Sum(r => r.Quantity.Float());

        var changed = (int)matAmount;

        _materialStorage.TryChangeMaterialAmount(ent, ent.Comp.ExtractedMaterial, changed);
        if (success)
        {
            _audio.PlayPvs(ent.Comp.ExtractSound, ent);
        }
        QueueDel(used);

        return true;
    }
}

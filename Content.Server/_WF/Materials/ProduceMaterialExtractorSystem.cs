using System.Linq;
using Content.Server.Botany.Components;
using Content.Server.Materials.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared.Storage.Components;

namespace Content.Server.Materials;

public sealed partial class ProduceMaterialExtractorSystem
{
    private void OnGetDumpableVerb(Entity<ProduceMaterialExtractorComponent> ent, ref GetDumpableVerbEvent args)
    {
        if (!this.IsPowered(ent, EntityManager))
            return;

        args.Verb = Loc.GetString("dump-biogenerator-verb-name", ("unit", ent));
    }

    private void OnDump(Entity<ProduceMaterialExtractorComponent> ent, ref DumpEvent args)
    {
        if (args.Handled)
            return;

        if (!this.IsPowered(ent, EntityManager))
            return;

        args.Handled = true;

        bool success = false;

        foreach (var item in args.DumpQueue)
        {
            if (TryExtractFromProduce(ent, item, args.User))
                success = true;
        }

        if (success)
        {
            args.PlaySound = true;
        }
    }

    private bool TryExtractFromProduce(Entity<ProduceMaterialExtractorComponent> ent, EntityUid used, EntityUid user)
    {
        if (!TryComp<ProduceComponent>(used, out var produce))
            return false;

        if (!_solutionContainer.TryGetSolution(used, produce.SolutionName, out var solution))
            return false;

        var matAmount = solution.Value.Comp.Solution.Contents
            .Where(r => ent.Comp.ExtractionReagents.Contains(r.Reagent.Prototype))
            .Sum(r => r.Quantity.Float());

        var changed = (int)matAmount;

        if (changed == 0)
        {
            _popup.PopupEntity(Loc.GetString("material-extractor-comp-wrongreagent", ("used", used)), user, user);
            return false;
        }

        _materialStorage.TryChangeMaterialAmount(ent, ent.Comp.ExtractedMaterial, changed);

        QueueDel(used);

        return true;
    }
}

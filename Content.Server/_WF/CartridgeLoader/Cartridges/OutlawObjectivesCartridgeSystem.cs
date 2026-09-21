using Content.Server._WF.OutlawObjectives;
using Content.Server.CartridgeLoader;
using Content.Shared._WF.CartridgeLoader.Cartridges;
using Content.Shared._WF.OutlawObjectives;
using Content.Shared.CartridgeLoader;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.CartridgeLoader.Cartridges;

[RegisterComponent]
public sealed partial class OutlawObjectivesCartridgeComponent : Component;

public sealed class OutlawObjectivesCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OutlawObjectivesCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<OutlawObjectivesChangedEvent>(OnObjectivesChanged);
    }

    private void OnUiReady(Entity<OutlawObjectivesCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        _cartridgeLoader.UpdateCartridgeUiState(args.Loader, BuildState());
    }

    private void OnObjectivesChanged(ref OutlawObjectivesChangedEvent ev)
    {
        OutlawObjectivesUiState? state = null;

        var query = EntityQueryEnumerator<OutlawObjectivesCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out var uid, out _, out var cartridge))
        {
            // A closed PDA keeps its program active, so without this the app is sent updates nobody is looking at.
            if (cartridge.LoaderUid is not { } loader
                || !TryComp<CartridgeLoaderComponent>(loader, out var loaderComp)
                || loaderComp.ActiveProgram != uid
                || !_ui.IsUiOpen(loader, loaderComp.UiKey))
            {
                continue;
            }

            _cartridgeLoader.UpdateCartridgeUiState(loader, state ??= BuildState(), loader: loaderComp);
        }
    }

    private OutlawObjectivesUiState BuildState()
    {
        var entries = new List<OutlawObjectiveEntry>();

        var query = EntityQueryEnumerator<OutlawObjectiveComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Open.Count == 0 || TerminatingOrDeleted(uid))
                continue;

            var dead = _mobState.IsDead(uid);
            var ssd = !HasComp<ActorComponent>(uid);
            var name = Name(uid);

            foreach (var objective in comp.Open)
            {
                var proto = _proto.Index(objective);

                if (dead && proto.Trigger == OutlawObjectiveTrigger.Critical)
                    continue;

                entries.Add(new OutlawObjectiveEntry(name, objective, ssd));
            }
        }

        // Without this the objective vanishes from the app with the body.
        var items = EntityQueryEnumerator<OutlawObjectiveItemComponent>();
        while (items.MoveNext(out var uid, out var item))
        {
            var owner = item.OwningCharacter;

            if (TerminatingOrDeleted(uid)
                || (!TerminatingOrDeleted(owner) && HasComp<OutlawObjectiveComponent>(owner)))
            {
                continue;
            }

            entries.Add(new OutlawObjectiveEntry(item.OwnerName, item.Objective, false));
        }

        // The query order is not stable, so without this the entries shuffle under whoever is reading the app.
        entries.Sort(CompareEntries);

        return new OutlawObjectivesUiState(entries);
    }

    private static int CompareEntries(OutlawObjectiveEntry a, OutlawObjectiveEntry b)
    {
        var byTarget = string.CompareOrdinal(a.Target, b.Target);

        return byTarget != 0 ? byTarget : string.CompareOrdinal(a.Objective, b.Objective);
    }
}

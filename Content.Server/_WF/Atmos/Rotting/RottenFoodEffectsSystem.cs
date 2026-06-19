using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Nutrition;
using Content.Server.Temperature.Components;
using Content.Shared.Atmos.Rotting;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Damage;
using Content.Shared.Nutrition.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.Atmos.Rotting;

public sealed class RottenFoodEffectsSystem : EntitySystem
{
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;

    private static readonly ProtoId<ReagentPrototype> ToxinReagent = "GastroToxin";
    private const string RotDamageType = "Rot";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RottingComponent, ComponentStartup>(OnRottingStartup);
        SubscribeLocalEvent<RottingComponent, FoodSlicedEvent>(OnRottingFoodSliced);
        SubscribeLocalEvent<EdibleComponent, BeforeDamageChangedEvent>(OnFoodDamage);
        SubscribeLocalEvent<EdibleComponent, IsRottingEvent>(OnFoodIsRotting);
    }

    private void OnFoodDamage(EntityUid uid, EdibleComponent component, ref BeforeDamageChangedEvent args)
    {
        if (args.Damage.DamageDict.ContainsKey(RotDamageType))
            args.Cancelled = true;
    }

    private void OnFoodIsRotting(EntityUid uid, EdibleComponent component, ref IsRottingEvent args)
    {
        // For anything that has its own AtmosExposed (e.g. for cooking).
        if (HasComp<AtmosExposedComponent>(uid))
            return;

        if (!TryComp<TemperatureComponent>(uid, out var temp))
            return;

        var air = _atmosphere.GetContainingMixture((uid, Transform(uid)));
        if (air != null)
            temp.CurrentTemperature = air.Temperature;
    }

    private void OnRottingFoodSliced(EntityUid uid, RottingComponent component, ref FoodSlicedEvent args)
    {
        EnsureComp<RottingComponent>(args.Slice);
    }

    private void OnRottingStartup(EntityUid uid, RottingComponent component, ComponentStartup args)
    {
        if (!TryComp<EdibleComponent>(uid, out var edible))
            return;

        if (!_solutionContainer.TryGetSolution(uid, edible.Solution, out var soln, out var solution))
            return;

        var removed = _solutionContainer.SplitSolution(soln.Value, solution.Volume);
        _solutionContainer.TryAddReagent(soln.Value, ToxinReagent, removed.Volume);
    }
}

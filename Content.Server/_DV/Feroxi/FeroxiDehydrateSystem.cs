using System.Linq;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Nutrition.Components;

namespace Content.Server._DV.Feroxi;

public sealed class FeroxiDehydrateSystem : EntitySystem
{
    [Dependency] private readonly BodySystem _body = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FeroxiDehydrateComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);
    }

    private void OnRefreshMovespeed(EntityUid uid, FeroxiDehydrateComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        if (!TryComp<ThirstComponent>(uid, out var thirst))
        {
            return;
        }
        // OverHydrated: 600
        // Okay: 450
        // Thirsty: 300
        // Parched: 150
        // else: 0
        float speedMod;
        if (thirst.CurrentThirst >= thirst.ThirstThresholds[ThirstThreshold.OverHydrated])
        {
            speedMod = component.OverhydratedModifier;
        }
        else if (thirst.CurrentThirst >= thirst.ThirstThresholds[ThirstThreshold.Okay])
        {
            speedMod = component.OkayModifier;
        }
        else if (thirst.CurrentThirst >= thirst.ThirstThresholds[ThirstThreshold.Thirsty])
        {
            speedMod = component.ThirstyModifier;
        }
        else if (thirst.CurrentThirst >= thirst.ThirstThresholds[ThirstThreshold.Parched])
        {
            speedMod = component.ParchedModifier;
        }
        else
        {
            speedMod = component.DehydratedModifier;
        }
        args.ModifySpeed(speedMod, speedMod);
    }
}

    // public override void Update(float frameTime)
    // {
    //     var query = EntityQueryEnumerator<FeroxiDehydrateComponent, ThirstComponent>();
    //
    //     while (query.MoveNext(out var uid, out var feroxiDehydrate, out var thirst))
    //     {
    //         var currentThirst = thirst.CurrentThirst;
    //         var shouldBeDehydrated = currentThirst <= feroxiDehydrate.DehydrationThreshold;
    //
    //         if (feroxiDehydrate.Dehydrated != shouldBeDehydrated)
    //         {
    //             UpdateDehydrationStatus((uid, feroxiDehydrate), shouldBeDehydrated);
    //         }
    //     }
    // }

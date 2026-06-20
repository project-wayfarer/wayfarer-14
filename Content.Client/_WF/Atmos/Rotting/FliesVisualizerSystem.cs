// Adds an animated flies sprite layer to edible entities when they gain RottingComponent.
using Content.Shared.Atmos.Rotting;
using Content.Shared.Nutrition.Components;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._WF.Atmos.Rotting;

public sealed class FliesVisualizerSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly ResPath FliesSprite = new("Objects/Misc/flies.rsi");
    private const string FliesState = "flies";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RottingComponent, ComponentStartup>(OnRottingStartup);
    }

    private void OnRottingStartup(EntityUid uid, RottingComponent component, ComponentStartup args)
    {
        // Restrict to edible entities so dead mob bodies don't sprout flies.
        // Could change this later if we want that.
        if (!HasComp<EdibleComponent>(uid))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        _sprite.LayerMapReserve((uid, sprite), FliesVisualLayers.Flies);
        _sprite.LayerSetRsi((uid, sprite), FliesVisualLayers.Flies, FliesSprite);
        _sprite.LayerSetRsiState((uid, sprite), FliesVisualLayers.Flies, FliesState);
        _sprite.LayerSetVisible((uid, sprite), FliesVisualLayers.Flies, true);
    }
}

public enum FliesVisualLayers : byte
{
    Flies
}

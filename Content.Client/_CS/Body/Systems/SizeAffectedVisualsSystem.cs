using System.Collections.Generic;
using Content.Shared.Body.Components;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;

namespace Content.Client.Body.Systems;

/// <summary>
/// Handles visual scaling for entities affected by size manipulation.
/// </summary>
public sealed class SizeAffectedVisualsSystem : EntitySystem
{
    private readonly Dictionary<EntityUid, float> _baseScales = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SizeAffectedComponent, ComponentStartup>(OnComponentStartup);
        SubscribeLocalEvent<SizeAffectedComponent, AfterAutoHandleStateEvent>(OnHandleState);
        SubscribeLocalEvent<SizeAffectedComponent, ComponentShutdown>(OnComponentShutdown);
        
        Logger.Info("SizeAffectedVisualsSystem initialized!");
    }

    private void OnComponentStartup(EntityUid uid, SizeAffectedComponent component, ComponentStartup args)
    {
        Logger.Info($"SizeAffectedVisuals: ComponentStartup for {ToPrettyString(uid)}");
        
        // Store the original scale when the component is first added
        if (!TryComp<SpriteComponent>(uid, out var sprite))
        {
            Logger.Warning($"SizeAffectedVisuals: No sprite component found for {ToPrettyString(uid)}");
            return;
        }

        // Store the current scale as the base scale for this entity
        if (!_baseScales.ContainsKey(uid))
        {
            _baseScales[uid] = sprite.Scale.X;
            Logger.Info($"SizeAffectedVisuals: Stored base scale {sprite.Scale.X} for {ToPrettyString(uid)}");
        }

        UpdateScale(uid, component, sprite);
    }

    private void OnHandleState(EntityUid uid, SizeAffectedComponent component, ref AfterAutoHandleStateEvent args)
    {
        Logger.Info($"SizeAffectedVisuals: OnHandleState for {ToPrettyString(uid)}, scale multiplier: {component.ScaleMultiplier}");
        
        if (!TryComp<SpriteComponent>(uid, out var sprite))
        {
            Logger.Warning($"SizeAffectedVisuals: No sprite component found in OnHandleState for {ToPrettyString(uid)}");
            return;
        }

        UpdateScale(uid, component, sprite);
    }

    private void OnComponentShutdown(EntityUid uid, SizeAffectedComponent component, ComponentShutdown args)
    {
        // Clean up stored base scale
        _baseScales.Remove(uid);
    }

    private void UpdateScale(EntityUid uid, SizeAffectedComponent component, SpriteComponent sprite)
    {
        var baseScale = _baseScales.GetValueOrDefault(uid, 1.0f);
        var scale = component.ScaleMultiplier * baseScale;
        var oldScale = sprite.Scale;
        sprite.Scale = new System.Numerics.Vector2(scale, scale);
        Logger.Info($"SizeAffectedVisuals: Updated scale for {ToPrettyString(uid)} from {oldScale} to {sprite.Scale} (multiplier: {component.ScaleMultiplier}, base: {baseScale})");
    }
}

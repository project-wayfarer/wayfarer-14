using System;
using System.Numerics;
using Content.Shared._WF.Clown;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._WF.Clown;

public sealed class JugglingOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly SpriteSystem _sprites;

    // Drawn below the lighting pass so the items darken with the room.
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly EntityQuery<MetaDataComponent> _metaQuery;

    private const float Cycle = 1.4f;
    private const float ItemScale = 0.65f;

    public JugglingOverlay(IEntityManager entities, IGameTiming timing, SpriteSystem sprites)
    {
        _entities = entities;
        _timing = timing;
        _sprites = sprites;
        _metaQuery = entities.GetEntityQuery<MetaDataComponent>();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var handle = args.WorldHandle;
        var curTime = (float)_timing.CurTime.TotalSeconds;

        var enumerator = _entities.EntityQueryEnumerator<JugglingActiveComponent, TransformComponent>();
        while (enumerator.MoveNext(out _, out var active, out var xform))
        {
            if (xform.MapID != args.MapId || active.JuggledItems.Count == 0)
                continue;

            var worldPos = xform.WorldPosition;
            var startTime = (float)active.StartTime.TotalSeconds;
            var elapsed = curTime - startTime;
            var n = active.JuggledItems.Count;

            for (var i = 0; i < n; i++)
            {
                // Stagger items evenly through the cycle so they do not overlap.
                var phase = i * (Cycle / n);
                var itemPos = ComputeItemPos(worldPos, elapsed, phase, Cycle);

                if (!_entities.TryGetEntity(active.JuggledItems[i], out var itemEnt))
                    continue;

                if (!_metaQuery.TryGetComponent(itemEnt.Value, out var meta) || meta.EntityPrototype == null)
                    continue;

                // The item is in the hidden juggle container, so the game is not drawing its
                // sprite. Its own icon is drawn here instead.
                var texture = _sprites.Frame0(meta.EntityPrototype);
                var halfSize = texture.Size / (2f * EyeManager.PixelsPerMeter) * ItemScale;

                var spinDir = (i % 2 == 0) ? 1.0 : -1.0;
                var spin = new Angle(elapsed * 1.5 * spinDir + i * 2.3);

                var box = new Box2(itemPos.X - halfSize.X, itemPos.Y - halfSize.Y, itemPos.X + halfSize.X, itemPos.Y + halfSize.Y);
                handle.DrawTextureRect(texture, new Box2Rotated(box, spin, itemPos));
            }
        }
    }

    // Each item rises in a tall arc from one hand to the other, then makes a quick low pass back.
    private static Vector2 ComputeItemPos(Vector2 center, float elapsed, float phase, float cycle)
    {
        var tNorm = ((elapsed + phase) % cycle) / cycle;
        if (tNorm < 0f) tNorm += 1f;

        var left  = center + new Vector2(-0.35f, 0f);
        var right = center + new Vector2( 0.35f, 0f);

        float x, y;
        if (tNorm < 0.55f)
        {
            // Tall arc from right hand to left hand. Peak height 0.9 tiles.
            var u = tNorm / 0.55f;
            x = MathHelper.Lerp(right.X, left.X, u);
            y = center.Y + 0.9f * MathF.Sin(MathF.PI * u);
        }
        else
        {
            // Short low pass from left hand back to right hand. Peak height 0.18 tiles.
            var u = (tNorm - 0.55f) / 0.45f;
            x = MathHelper.Lerp(left.X, right.X, u);
            y = center.Y + 0.18f * MathF.Sin(MathF.PI * u);
        }

        return new Vector2(x, y);
    }
}

// On juggle start and stop it toggles the player's walk state
// so the walk icon and move speed update as if they had pressed the walk key.
public sealed class JugglingVisualsSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new JugglingOverlay(EntityManager, _timing, _sprites));

        SubscribeLocalEvent<JugglingActiveComponent, ComponentStartup>(OnActiveStartup);
        SubscribeLocalEvent<JugglingActiveComponent, ComponentShutdown>(OnActiveShutdown);

        // Runs before the normal walk handling. While the local player is juggling
        // the walk key is consumed, so pressing it does nothing.
        CommandBinds.Builder
            .BindBefore(EngineKeyFunctions.Walk, new JuggleWalkBlocker(), typeof(SharedMoverController))
            .Register<JugglingVisualsSystem>();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<JugglingOverlay>();
        CommandBinds.Unregister<JugglingVisualsSystem>();
    }

    private void OnActiveStartup(Entity<JugglingActiveComponent> ent, ref ComponentStartup args)
        => DeferWalk(ent.Owner, true);

    private void OnActiveShutdown(Entity<JugglingActiveComponent> ent, ref ComponentShutdown args)
        => DeferWalk(ent.Owner, false);

    // Applying that walk state at the instant juggling starts or stops does not
    // update the walk icon or move speed, so it is applied once juggling has fully started or stopped instead.
    private void DeferWalk(EntityUid owner, bool walking)
        => Timer.Spawn(0, () => ApplyWalk(owner, walking));

    private void ApplyWalk(EntityUid uid, bool walking)
    {
        if (Deleted(uid))
            return;
        if (!TryComp<InputMoverComponent>(uid, out var mover))
            return;

        _mover.SetSprinting((uid, mover), 0, walking);
    }
}

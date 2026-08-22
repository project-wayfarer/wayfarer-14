using System.Numerics;
using Content.Shared._WF.Clown;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Enums;
using Robust.Shared.Input;
using Robust.Shared.Input.Binding;
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
        var rotation = args.Viewport.Eye?.Rotation ?? Angle.Zero;
        var rotationMatrix = Matrix3Helpers.CreateRotation(-rotation);

        var enumerator = _entities.EntityQueryEnumerator<JugglingActiveComponent, TransformComponent>();
        while (enumerator.MoveNext(out _, out var active, out var xform))
        {
            if (xform.MapID != args.MapId || active.JuggledItems.Count == 0)
                continue;

            var worldPos = xform.WorldPosition;
            var elapsed = curTime - (float)active.StartTime.TotalSeconds;
            var n = active.JuggledItems.Count;

            var worldMatrix = Matrix3Helpers.CreateTranslation(worldPos);
            handle.SetTransform(Matrix3x2.Multiply(rotationMatrix, worldMatrix));

            for (var i = 0; i < n; i++)
            {
                // Stagger items evenly through the cycle so they do not overlap.
                var itemPos = ComputeItemPos(elapsed + i * (Cycle / n));

                if (!_entities.TryGetEntity(active.JuggledItems[i], out var itemEnt)
                    || !_metaQuery.TryGetComponent(itemEnt.Value, out var meta) || meta.EntityPrototype == null)
                    continue;

                // The item is in the hidden juggle container, so the game is not drawing its
                // sprite. Its own icon is drawn here instead.
                var texture = _sprites.Frame0(meta.EntityPrototype);

                var spinDir = (i % 2 == 0) ? 1.0 : -1.0;
                var spin = new Angle(elapsed * 1.5 * spinDir + i * 2.3);

                var box = Box2.CenteredAround(itemPos, texture.Size / EyeManager.PixelsPerMeter * ItemScale);
                handle.DrawTextureRect(texture, new Box2Rotated(box, spin, itemPos));
            }
        }

        handle.SetTransform(Matrix3x2.Identity);
    }

    // Each item rises in a tall arc from one hand to the other, then makes a quick low pass back.
    // Positions are offsets from the player.
    private static Vector2 ComputeItemPos(float time)
    {
        var tNorm = (time % Cycle) / Cycle;
        if (tNorm < 0f) tNorm += 1f;

        const float left = -0.35f;
        const float right = 0.35f;

        // A tall arc from right hand to left for most of the cycle, then a short low pass back.
        var (from, to, height, start, span) = tNorm < 0.55f
            ? (right, left, 0.9f, 0f, 0.55f)
            : (left, right, 0.18f, 0.55f, 0.45f);

        var u = (tNorm - start) / span;
        return new Vector2(MathHelper.Lerp(from, to, u), height * MathF.Sin(MathF.PI * u));
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
        if (TryComp<InputMoverComponent>(uid, out var mover))
            _mover.SetSprinting((uid, mover), 0, walking);
    }
}

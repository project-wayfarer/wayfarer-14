using System;
using System.Collections.Generic;
using System.Numerics;
using Content.Shared._WF.Clown;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameObjects;
using Robust.Shared.Graphics;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Client._WF.Clown;

public sealed class BalloonStringOverlay : Overlay
{
    private readonly IEntityManager _entities;
    private readonly IGameTiming _timing;
    private readonly SpriteSystem _sprites;
    private readonly BalloonStringSystem _system;

    // Drawn below the lighting pass so the balloon and string darken with the room.
    public override OverlaySpace Space => OverlaySpace.WorldSpaceBelowFOV;

    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly EntityQuery<MetaDataComponent> _metaQuery;

    private TimeSpan _lastPhysicsTime;

    private const int StringSegments = 10;
    private readonly Vector2[] _stringVerts = new Vector2[2 * (StringSegments + 1)];

    private const float Buoyancy = 8f;
    private const float Drag = 1.5f;
    private const float StringLength = 0.9f;
    private const float MaxSpeed = 4f;

    // The balloon hangs from the carrier's hand, so the draw needs the hand's on-screen position.
    // These values are the hand offset from the body's centre, measured in tiles.
    private const float HandSide = 0.22f;
    private const float HandFrontEW = 0.05f;
    private const float HandBackEW = -0.05f;
    private const float HandVertical = -0.05f;

    private const float SpriteScale = 0.9f;
    private const float StringWidth = 0.03f;
    private const float StringSagFactor = 0.08f;
    private const float StringTrailFactor = 0.06f;
    private const float MaxTilt = 0.4f;
    private const float TiltFactor = 0.25f;

    private static readonly Color StringColor = new(0.94f, 0.91f, 0.85f, 0.9f);

    public BalloonStringOverlay(IEntityManager entities, IGameTiming timing, SpriteSystem sprites, BalloonStringSystem system)
    {
        _entities = entities;
        _timing = timing;
        _sprites = sprites;
        _system = system;
        _xformQuery = entities.GetEntityQuery<TransformComponent>();
        _metaQuery = entities.GetEntityQuery<MetaDataComponent>();
        _lastPhysicsTime = timing.CurTime;
    }

    // Runs once every frame. Returns when nobody is holding a balloon,
    // so it costs nothing the rest of the time.
    protected override void Draw(in OverlayDrawArgs args)
    {
        if (_system.Held.Count == 0)
            return;

        var handle = args.WorldHandle;
        var curTime = _timing.CurTime;

        // Cap the time step at 1/20 of a second. A brief stutter would otherwise fling the balloon far past the string length in one update.
        var dt = (float)Math.Clamp((curTime - _lastPhysicsTime).TotalSeconds, 0, 0.05);
        _lastPhysicsTime = curTime;

        foreach (var (balloonUid, info) in _system.Held)
        {
            if (!_xformQuery.TryGetComponent(info.Carrier, out var xform))
                continue;
            if (xform.MapID != args.MapId)
                continue;
            if (!_metaQuery.TryGetComponent(balloonUid, out var meta) || meta.EntityPrototype == null)
                continue;

            var carrierPos = xform.WorldPosition;
            var facing = xform.LocalRotation.GetCardinalDir();
            var isLeftHand = info.Location == HandLocation.Left;
            var handPos = carrierPos + GetHandOffset(facing, isLeftHand);

            var state = _system.GetOrInitState(balloonUid, handPos);

            var accel = new Vector2(0f, Buoyancy) - state.Vel * Drag;
            state.Vel += accel * dt;
            state.Pos += state.Vel * dt;

            var toBalloon = state.Pos - handPos;
            var dist = toBalloon.Length();
            if (dist > StringLength)
            {
                var outDir = toBalloon / dist;
                state.Pos = handPos + outDir * StringLength;
                var outwardVel = Vector2.Dot(state.Vel, outDir);
                if (outwardVel > 0f)
                    state.Vel -= outDir * outwardVel;
            }

            var speedSq = state.Vel.LengthSquared();
            if (speedSq > MaxSpeed * MaxSpeed)
                state.Vel *= MaxSpeed / MathF.Sqrt(speedSq);

            _system.SetState(balloonUid, state);

            DrawString(handle, handPos, state.Pos, state.Vel);

            // This overlay draws the floating balloon separately from the held item, so it
            // needs the balloon's own icon to draw with.
            var texture = _sprites.Frame0(meta.EntityPrototype);
            var halfSize = texture.Size / (2f * EyeManager.PixelsPerMeter) * SpriteScale;
            var tilt = new Angle(Math.Clamp(state.Vel.X * TiltFactor, -MaxTilt, MaxTilt));
            var box = new Box2(state.Pos.X - halfSize.X, state.Pos.Y - halfSize.Y,
                               state.Pos.X + halfSize.X, state.Pos.Y + halfSize.Y);
            handle.DrawTextureRect(texture, new Box2Rotated(box, tilt, state.Pos));
        }
    }

    private static Vector2 GetHandOffset(Direction facing, bool leftHand)
    {
        return facing switch
        {
            Direction.South => new Vector2(leftHand ? HandSide : -HandSide, HandVertical),
            Direction.North => new Vector2(leftHand ? -HandSide : HandSide, HandVertical),
            Direction.East  => new Vector2(leftHand ? HandFrontEW : HandBackEW, HandVertical),
            Direction.West  => new Vector2(leftHand ? -HandBackEW : -HandFrontEW, HandVertical),
            _               => new Vector2(0f, HandVertical),
        };
    }

    // Draws the string as a curved line from the hand to the balloon, shaped by one bend point.
    // The bend point is lowered so the string droops, and pushed to the side so the string
    // lags behind when the balloon swings.
    private void DrawString(DrawingHandleWorld handle, Vector2 hand, Vector2 balloon, Vector2 balloonVel)
    {
        var line = balloon - hand;
        var lineLen = line.Length();
        if (lineLen < 0.01f)
            return;

        var unit = line / lineLen;
        var perpAxis = new Vector2(-unit.Y, unit.X);
        var perpSpeed = Vector2.Dot(balloonVel, perpAxis);

        var control = (hand + balloon) * 0.5f
                      + new Vector2(0f, -StringSagFactor * lineLen)
                      - perpAxis * perpSpeed * StringTrailFactor;

        // Build the string as a thin band that follows the curve.
        var halfW = StringWidth * 0.5f;
        for (var i = 0; i <= StringSegments; i++)
        {
            var t = i / (float)StringSegments;
            var p = QuadBezier(hand, control, balloon, t);
            var tangent = QuadBezierTangent(hand, control, balloon, t);
            var n = new Vector2(-tangent.Y, tangent.X);
            var nLenSq = n.LengthSquared();
            if (nLenSq > 0.0001f)
                n *= halfW / MathF.Sqrt(nLenSq);
            _stringVerts[i * 2] = p + n;
            _stringVerts[i * 2 + 1] = p - n;
        }
        handle.DrawPrimitives(DrawPrimitiveTopology.TriangleStrip, _stringVerts, StringColor);
    }

    private static Vector2 QuadBezier(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        var omt = 1f - t;
        return omt * omt * p0 + 2f * omt * t * p1 + t * t * p2;
    }

    private static Vector2 QuadBezierTangent(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        return 2f * (1f - t) * (p1 - p0) + 2f * t * (p2 - p1);
    }
}

// Keeps the list of balloons that are currently held, updated when a balloon is picked up or dropped.
// The overlay reads this list instead of checking every balloon each frame.
public sealed class BalloonStringSystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public struct HeldInfo
    {
        public EntityUid Carrier;
        public HandLocation Location;
    }

    public struct BalloonPhysicsState
    {
        public Vector2 Pos;
        public Vector2 Vel;
    }

    public readonly Dictionary<EntityUid, HeldInfo> Held = new();
    private readonly Dictionary<EntityUid, BalloonPhysicsState> _states = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BalloonOnStringComponent, GotEquippedHandEvent>(OnEquipped);
        SubscribeLocalEvent<BalloonOnStringComponent, GotUnequippedHandEvent>(OnUnequipped);
        SubscribeLocalEvent<BalloonOnStringComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<BalloonOnStringComponent, ComponentShutdown>(OnShutdown);
        _overlays.AddOverlay(new BalloonStringOverlay(EntityManager, _timing, _sprites, this));
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<BalloonStringOverlay>();
    }

    private void OnEquipped(Entity<BalloonOnStringComponent> ent, ref GotEquippedHandEvent args)
    {
        Held[ent.Owner] = new HeldInfo { Carrier = args.User, Location = args.Hand.Location };
    }

    // When the balloon leaves the hand, forget where it had floated to. If it is picked up again
    // it should start hanging still from the hand, not jump back to its old position.
    private void OnUnequipped(Entity<BalloonOnStringComponent> ent, ref GotUnequippedHandEvent args)
        => Forget(ent.Owner);

    // Catches the case of a client entering view of a balloon that is already in someone's hand,
    // where the container-inserted event may have already passed before this component is set up locally.
    private void OnStartup(Entity<BalloonOnStringComponent> ent, ref ComponentStartup args)
    {
        if (!_containers.TryGetContainingContainer(ent.Owner, out var container))
            return;

        var carrier = container.Owner;
        if (!_hands.IsHolding(carrier, ent.Owner, out var handName))
            return;

        if (!_hands.TryGetHand(carrier, handName, out var hand))
            return;

        Held[ent.Owner] = new HeldInfo { Carrier = carrier, Location = hand.Value.Location };
    }

    private void OnShutdown(Entity<BalloonOnStringComponent> ent, ref ComponentShutdown args)
        => Forget(ent.Owner);

    private void Forget(EntityUid balloon)
    {
        Held.Remove(balloon);
        _states.Remove(balloon);
    }

    // A balloon with no saved state starts at rest 0.9 tiles above the hand.
    public BalloonPhysicsState GetOrInitState(EntityUid balloon, Vector2 handPos)
    {
        if (_states.TryGetValue(balloon, out var state))
            return state;

        state = new BalloonPhysicsState
        {
            Pos = handPos + new Vector2(0f, 0.9f),
            Vel = Vector2.Zero,
        };
        _states[balloon] = state;
        return state;
    }

    public void SetState(EntityUid balloon, BalloonPhysicsState state)
    {
        _states[balloon] = state;
    }
}

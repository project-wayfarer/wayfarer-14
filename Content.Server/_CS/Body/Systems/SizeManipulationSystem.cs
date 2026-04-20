
using System.Numerics;
using Content.Server.Consent;
using Content.Shared._CS.Body.Components;
using Content.Shared.Consent;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared._CS.Weapons.Ranged.Components;
using Content.Shared.Examine;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._CS.Body.Systems;

public sealed class SizeManipulationSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ConsentSystem _consent = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    private ISawmill _sawmill = default!;

    private static readonly ProtoId<ConsentTogglePrototype> SizeManipulationConsent = "SizeManipulation";

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("size_manipulator");

        SubscribeLocalEvent<SizeAffectedComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, SizeAffectedComponent component, ExaminedEvent args)
    {
        // Only show if entity has been resized (scale is not 1.0)
        if (Math.Abs(component.ScaleMultiplier - 1.0f) < 0.001f)
            return;

        var totalScale = component.ScaleMultiplier * component.BaseScale;
        
        string message;
        if (totalScale > 1.0f)
        {
            message = Loc.GetString("size-manipulator-examine-bigger", [("target", uid), ("scale", totalScale.ToString("F2"))]);
        }
        else
        {
            message = Loc.GetString("size-manipulator-examine-smaller", [("target", uid), ("scale", totalScale.ToString("F2"))]);
        }

        args.PushMarkup($"[color=gray]{message}[/color]");
    }

    /// <summary>
    /// Applies a size change to the target entity
    /// </summary>
    public bool TryChangeSize(EntityUid target, SizeManipulatorMode mode, EntityUid? user = null, bool safetyDisabled = false)
    {
        // Only allow size manipulation on mobs (living entities)
        if (!HasComp<MobStateComponent>(target))
        {
            _sawmill.Debug($"SizeManipulation: Target {ToPrettyString(target)} is not a mob, ignoring");
            return false;
        }

        // Check consent
        if (!_consent.HasConsent(target, SizeManipulationConsent))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("size-manipulator-consent-denied"), target, user.Value);

            _sawmill.Debug($"SizeManipulation: Consent denied for {ToPrettyString(target)}");
            return false;
        }

        var sizeComp = EnsureComp<SizeAffectedComponent>(target);

        _sawmill.Debug($"SizeManipulation: TryChangeSize called on {ToPrettyString(target)}, mode: {mode}, current scale: {sizeComp.ScaleMultiplier}, safety disabled: {safetyDisabled}");

        // If safety is disabled, double the max limit
        var maxScale = safetyDisabled ? sizeComp.MaxScale * 2.0f : sizeComp.MaxScale;

        float newScale;
        if (mode == SizeManipulatorMode.Grow)
        {
            newScale = sizeComp.ScaleMultiplier + sizeComp.ScaleChangeAmount;
            if (newScale > maxScale)
            {
                if (user != null)
                    _popup.PopupEntity(Loc.GetString("size-manipulator-max-size"), target, user.Value);
                return false;
            }
        }
        else
        {
            newScale = sizeComp.ScaleMultiplier - sizeComp.ScaleChangeAmount;
            if (newScale < sizeComp.MinScale)
            {
                if (user != null)
                    _popup.PopupEntity(Loc.GetString("size-manipulator-min-size"), target, user.Value);
                return false;
            }
        }

        sizeComp.ScaleMultiplier = newScale;
        Dirty(target, sizeComp);

        // Apply physics scaling
        ApplyPhysicsScale(target, newScale, sizeComp.BaseScale);

        _sawmill.Debug($"SizeManipulation: Set scale multiplier to {newScale} for {ToPrettyString(target)}");

        // Visual scaling should be handled by a shared/client system that reads SizeAffectedComponent
        // Server should not directly manipulate sprite components

        var message = mode == SizeManipulatorMode.Grow
            ? Loc.GetString("size-manipulator-target-grow")
            : Loc.GetString("size-manipulator-target-shrink");

        _popup.PopupEntity(message, target, PopupType.Medium);

        return true;
    }

    /// <summary>
    /// Applies physics scaling to the target's fixtures
    /// </summary>
    private void ApplyPhysicsScale(EntityUid target, float scaleMultiplier, float baseScale)
    {
        if (!TryComp<FixturesComponent>(target, out var fixtures))
            return;

        if (!TryComp<SizeAffectedComponent>(target, out var sizeComp))
            return;

        var totalScale = scaleMultiplier * baseScale;

        foreach (var (id, fixture) in fixtures.Fixtures)
        {
            // Only scale hard fixtures (collision fixtures)
            if (!fixture.Hard)
                continue;

            // Store original density for mass scaling
            if (!sizeComp.OriginalFixtureDensities.ContainsKey(id))
            {
                sizeComp.OriginalFixtureDensities[id] = fixture.Density;
                _sawmill.Debug($"SizeManipulation: Stored original density {fixture.Density} for fixture {id}");
            }

            switch (fixture.Shape)
            {
                case PhysShapeCircle circle:
                    // Store original radius on first scaling
                    if (!sizeComp.OriginalFixtureRadii.ContainsKey(id))
                    {
                        sizeComp.OriginalFixtureRadii[id] = circle.Radius;
                        _sawmill.Debug($"SizeManipulation: Stored original radius {circle.Radius} for fixture {id}");
                    }

                    var originalRadius = sizeComp.OriginalFixtureRadii[id];
                    var newRadius = originalRadius * totalScale;

                    _physics.SetPositionRadius(target, id, fixture, circle, circle.Position * totalScale, newRadius, fixtures);
                    _sawmill.Debug($"SizeManipulation: Scaled circle fixture {id} radius from {circle.Radius} to {newRadius} (original: {originalRadius}, scale: {totalScale})");
                    break;

                case PolygonShape poly:
                    // Store original vertices on first scaling
                    if (!sizeComp.OriginalFixtureVertices.ContainsKey(id))
                    {
                        var verts = poly.Vertices;
                        var storedVerts = new Vector2[verts.Length];
                        for (int i = 0; i < verts.Length; i++)
                        {
                            storedVerts[i] = verts[i];
                        }
                        sizeComp.OriginalFixtureVertices[id] = storedVerts;
                        _sawmill.Debug($"SizeManipulation: Stored original polygon vertices for fixture {id}");
                    }

                    // Scale all vertices by the total scale from the original vertices
                    var originalVerts = sizeComp.OriginalFixtureVertices[id];
                    var scaledVerts = new Vector2[originalVerts.Length];

                    for (int i = 0; i < originalVerts.Length; i++)
                    {
                        scaledVerts[i] = originalVerts[i] * totalScale;
                    }

                    _physics.SetVertices(target, id, fixture, poly, scaledVerts, fixtures);
                    _sawmill.Debug($"SizeManipulation: Scaled polygon fixture {id} with {originalVerts.Length} vertices (scale: {totalScale})");
                    break;

                case PhysShapeAabb:
                    // AABBs are converted to PolygonShape, so this case should be rare
                    _sawmill.Debug($"SizeManipulation: Skipping AABB fixture {id} - convert to polygon first");
                    break;

                default:
                    _sawmill.Debug($"SizeManipulation: Skipping unsupported fixture {id} of type {fixture.Shape.GetType().Name}");
                    break;
            }

            // Scale density for mass scaling
            // Mass scales with volume (scale³), but since area scales with scale²,
            // we need to scale density by scale to achieve scale³ mass scaling
            var originalDensity = sizeComp.OriginalFixtureDensities[id];
            var newDensity = originalDensity * totalScale;

            // Only update density if it changed significantly
            if (Math.Abs(fixture.Density - newDensity) > 0.001f)
            {
                // Pass update: false to avoid recalculating mass multiple times
                _physics.SetDensity(target, id, fixture, newDensity, update: false, manager: fixtures);
                _sawmill.Debug($"SizeManipulation: Scaled density for fixture {id} from {originalDensity} to {newDensity} (scale: {totalScale})");
            }
        }

        // Recalculate mass data once after all fixtures have been scaled
        _physics.ResetMassData(target, fixtures);

        if (TryComp<PhysicsComponent>(target, out var physicsComp))
        {
            _sawmill.Debug($"SizeManipulation: New mass for {ToPrettyString(target)} is {physicsComp.Mass} kg (scale: {totalScale})");
        }
    }
}

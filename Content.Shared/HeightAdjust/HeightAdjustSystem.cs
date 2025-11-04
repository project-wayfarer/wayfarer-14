using System;
using System.Linq;
using System.Numerics;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Maths;

namespace Content.Shared.HeightAdjust;

public sealed class HeightAdjustSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedContentEyeSystem _eye = default!;
    [Dependency] private readonly SharedHumanoidAppearanceSystem _appearance = default!;
    [Dependency] private readonly IConfigurationManager _config = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HumanoidAppearanceComponent, RequestSizeRecalcEvent>(OnRequestSizeRecalc);
    }

    /// <summary>
    /// Handles requests to recalculate an entity's size by collecting all active modifiers
    /// and applying the final combined scale.
    /// </summary>
    private void OnRequestSizeRecalc(EntityUid target, HumanoidAppearanceComponent component, ref RequestSizeRecalcEvent ev)
    {
        // Collect all size modifiers from various systems
        var getModifiersEvent = new GetSizeModifierEvent(target);
        RaiseLocalEvent(target, ref getModifiersEvent);

        // Calculate final scale by multiplying all modifiers
        float finalScale = 1.0f;

        // Sort by priority (lower priority applied first, so higher priority can override)
        var sortedModifiers = getModifiersEvent.Modifiers.OrderBy(m => m.Priority).ToList();

        foreach (var modifier in sortedModifiers)
        {
            finalScale *= modifier.Scale;
        }

        // Apply the final scale, bypassing species limits for temporary effects
        SetScale(target, finalScale, bypassLimits: true);
    }


    /// <summary>
    ///     Changes the density of fixtures and zoom of eyes based on a provided float scale
    /// </summary>
    /// <param name="uid">The entity to modify values for</param>
    /// <param name="scale">The scale multiplier to apply to base height/width</param>
    /// <param name="bypassLimits">Whether to bypass species min/max limits (for temporary effects)</param>
    /// <returns>True if all operations succeeded</returns>
    public bool SetScale(EntityUid uid, float scale, bool bypassLimits = false)
    {
        if (!EntityManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var humanoid))
            return false;

        // Multiply the base height/width by the scale modifier
        var newHeight = humanoid.BaseHeight * scale;
        var newWidth = humanoid.BaseWidth * scale;

        _appearance.SetHeight(uid, newHeight, bypassLimits: bypassLimits, humanoid: humanoid);
        _appearance.SetWidth(uid, newWidth, bypassLimits: bypassLimits, humanoid: humanoid);

        return true;
    }

    /// <summary>
    ///     Changes the density of fixtures and zoom of eyes based on a provided Vector2 scale
    /// </summary>
    /// <param name="uid">The entity to modify values for</param>
    /// <param name="scale">The base scale to set (X = width, Y = height). This sets BaseHeight/BaseWidth.</param>
    /// <returns>True if all operations succeeded</returns>
    public bool SetScale(EntityUid uid, Vector2 scale)
    {
        if (!EntityManager.TryGetComponent<HumanoidAppearanceComponent>(uid, out var humanoid))
            return false;

        // This is setting the BASE scale from character customization
        // Update both base and current values
        humanoid.BaseWidth = scale.X;
        humanoid.BaseHeight = scale.Y;

        _appearance.SetScale(uid, scale, humanoid: humanoid);

        return true;
    }
}

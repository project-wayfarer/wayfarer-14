using System.Collections.Generic;
using System.Numerics;
using Robust.Shared.GameStates;

namespace Content.Shared._CS.Body.Components;

/// <summary>
/// Marks an entity as being affected by size manipulation.
/// Tracks the current scale multiplier applied to the entity.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SizeAffectedComponent : Component
{
    /// <summary>
    /// Current scale multiplier applied to this entity
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ScaleMultiplier = 1.0f;

    /// <summary>
    /// Minimum scale the entity can be shrunk to
    /// </summary>
    [DataField]
    public float MinScale = 0.25f;

    /// <summary>
    /// Maximum scale the entity can be grown to
    /// </summary>
    [DataField]
    public float MaxScale = 2.5f;

    /// <summary>
    /// How much to change scale per hit
    /// </summary>
    [DataField]
    public float ScaleChangeAmount = 0.15f;

    /// <summary>
    /// Base scale of the entity (used for calculations)
    /// </summary>
    [DataField]
    public float BaseScale = 1.0f;

    /// <summary>
    /// Stores original fixture radii for scaling calculations (fixture id -> original radius)
    /// </summary>
    [DataField]
    public Dictionary<string, float> OriginalFixtureRadii = new();

    /// <summary>
    /// Stores original fixture polygon vertices for scaling calculations (fixture id -> original vertices)
    /// </summary>
    [DataField]
    public Dictionary<string, Vector2[]> OriginalFixtureVertices = new();

    /// <summary>
    /// Stores original fixture densities for mass scaling calculations (fixture id -> original density)
    /// </summary>
    [DataField]
    public Dictionary<string, float> OriginalFixtureDensities = new();
}

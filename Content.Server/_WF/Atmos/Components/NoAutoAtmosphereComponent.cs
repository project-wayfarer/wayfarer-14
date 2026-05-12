namespace Content.Server._WF.Atmos.Components;

/// <summary>
/// Prevents <see cref="AutomaticAtmosSystem"/> from automatically adding a
/// <see cref="GridAtmosphereComponent"/> to this grid when its mass crosses the threshold.
/// Used on large asteroid dungeons (e.g. VGRoid) that don't need atmosphere simulation.
/// </summary>
[RegisterComponent]
public sealed partial class NoAutoAtmosphereComponent : Component;

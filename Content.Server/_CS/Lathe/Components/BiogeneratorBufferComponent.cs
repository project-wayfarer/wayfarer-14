using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._CS.Lathe.Components;

[RegisterComponent]
public sealed partial class BiogeneratorBufferComponent : Component
{
    /// <summary>
    /// Current amount of buffered biomass.
    /// </summary>
    [DataField("current")]
    public int CurrentBuffer = 0;

    /// <summary>
    /// Maximum buffer capacity.
    /// </summary>
    [DataField("max")]
    public int MaxBuffer = 50;

    /// <summary>
    /// Amount of biomass regenerated per interval.
    /// </summary>
    [DataField("regenAmount")]
    public int RegenAmount = 5;

    /// <summary>
    /// Time between regeneration ticks.
    /// </summary>
    [DataField("regenInterval")]
    public TimeSpan RegenInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Next time the buffer will be regenerated.
    /// </summary>
    [DataField("nextRegen", customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextRegen = TimeSpan.Zero;

    public bool Active = true;
}

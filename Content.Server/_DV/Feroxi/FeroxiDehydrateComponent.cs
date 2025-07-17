using Content.Shared.Body.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._DV.Feroxi;

/// <summary>
/// Component that allows the switching between <see cref="MetabolizerTypePrototype"/>s based on thirst
/// </summary>
[RegisterComponent, Access(typeof(FeroxiDehydrateSystem))]
public sealed partial class FeroxiDehydrateComponent : Component
{
    [DataField("overhydrated", required: true)]
    public float OverhydratedModifier = 1f;

    [DataField("okay", required: true)]
    public float OkayModifier = 0.9f;

    [DataField("thirsty", required: true)]
    public float ThirstyModifier = 0.8f;

    [DataField("parched", required: true)]
    public float ParchedModifier = 0.7f;

    [DataField("dehydrated", required: true)]
    public float DehydratedModifier = 0.5f;


}

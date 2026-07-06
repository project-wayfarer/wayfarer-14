namespace Content.Server._WF.Atmos.Components;

[RegisterComponent]
public sealed partial class InlineHeatExchangerComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("inlet")]
    public string InletName { get; set; } = "inlet";

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("outlet")]
    public string OutletName { get; set; } = "outlet";

    /// <summary>
    /// Thermal convection coefficient (J/degK/sec).
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("convectionCoefficient")]
    public float K { get; set; } = 8000f;

    /// <summary>
    /// Thermal radiation coefficient. Number of "effective" tiles this
    /// radiator radiates compared to superconductivity tile losses.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("radiationCoefficient")]
    public float alpha { get; set; } = 140f;
}


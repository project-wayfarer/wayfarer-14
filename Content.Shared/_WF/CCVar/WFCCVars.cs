using Robust.Shared.Configuration;

namespace Content.Shared._WF.CCVar;

[CVarDefs]
public sealed class WFCCVars
{
    /// <summary>
    /// The cost in spesos to found a new player corporation.
    /// </summary>
    public static readonly CVarDef<int> CorporationCreationCost =
        CVarDef.Create("wf.corporation.creation_cost", 1000000, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Maximum number of characters allowed in a corporation name.
    /// </summary>
    public static readonly CVarDef<int> CorporationNameMaxLength =
        CVarDef.Create("wf.corporation.name_max_length", 40, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Maximum number of characters allowed in a corporation description.
    /// </summary>
    public static readonly CVarDef<int> CorporationDescriptionMaxLength =
        CVarDef.Create("wf.corporation.description_max_length", 500, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Multiplier applied to the appraised grid value to calculate the corporation station upkeep cost per 4 hours.
    /// </summary>
    public static readonly CVarDef<float> StationUpkeepMultiplier =
        CVarDef.Create("wf.corporation.station_upkeep_multiplier", 1.5f, CVar.SERVER);

    /// <summary>
    /// Whether player corporations are allowed to purchase stations.
    /// </summary>
    public static readonly CVarDef<bool> CorporationStationPurchaseEnabled =
        CVarDef.Create("wf.corporation.station_purchase_enabled", false, CVar.SERVER);
}

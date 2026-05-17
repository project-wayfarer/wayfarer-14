using Robust.Shared.Configuration;

namespace Content.Shared._WF.CCVar;

/// <summary>
/// Contains CVars used by Wayfarer.
/// </summary>
[CVarDefs]
public sealed class WFCVars
{
    /// <summary>
    /// Anomaly research point multiplier. Default of 0.70 (70%) Lower than one is a penalty, higher than one is a bonus.
    /// </summary>
    public static readonly CVarDef<float> AnomalyPointMultiplier =
    CVarDef.Create("wf.research.anomaly_multiplier", 0.70f, CVar.SERVER | CVar.REPLICATED);

    /// <summary>
    /// Artifact research point multiplier. Default of 0.90 (90%) Lower than one is a penalty, higher than one is a bonus.
    /// </summary>
    public static readonly CVarDef<float> ArtifactPointMultiplier =
    CVarDef.Create("wf.research.artifact_multiplier", 0.90f, CVar.SERVER | CVar.REPLICATED);
}

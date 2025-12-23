using Robust.Shared.Configuration;

namespace Content.Shared.Floofstation.FSCVars;

/// <summary>
/// Floofstation cvars!
/// </summary>
[CVarDefs]
// Using Delta's to go off of, do not know if this will work.
public sealed class FSCVars
{
    public static readonly CVarDef<string> ConsentRules = CVarDef.Create("floof.consent_rules", "", CVar.ARCHIVE | CVar.CLIENTONLY);
    
    /// <summary>
    /// How many characters the consent text can be.
    /// </summary>
    public static readonly CVarDef<int> ConsentFreetextMaxLength = CVarDef.Create("consent.freetext_max_length", 1000, CVar.REPLICATED | CVar.SERVER);
}

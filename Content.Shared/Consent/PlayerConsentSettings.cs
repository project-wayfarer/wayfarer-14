using System.Linq;
using Content.Shared.Floofstation.FSCVars; // Flooftier
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Consent;

[Serializable, NetSerializable]
public sealed class PlayerConsentSettings
{
    public string Freetext;
    public string CharacterFreetext;
    public Dictionary<ProtoId<ConsentTogglePrototype>, string> Toggles;

    public PlayerConsentSettings()
    {
        Freetext = string.Empty;
        CharacterFreetext = string.Empty;
        Toggles = new Dictionary<ProtoId<ConsentTogglePrototype>, string>();
    }

    public PlayerConsentSettings(
        string freetext,
        string characterFreetext,
        Dictionary<ProtoId<ConsentTogglePrototype>, string> toggles)
    {
        Freetext = freetext;
        CharacterFreetext = characterFreetext;
        Toggles = toggles;
    }

    public void EnsureValid(IConfigurationManager configManager, IPrototypeManager prototypeManager)
    {
        var maxLength = configManager.GetCVar(FSCVars.ConsentFreetextMaxLength); // Flooftier
        Freetext = Freetext.Trim();
        if (Freetext.Length > maxLength)
            Freetext = Freetext.Substring(0, maxLength);

        CharacterFreetext = CharacterFreetext.Trim();
        if (CharacterFreetext.Length > maxLength)
            CharacterFreetext = CharacterFreetext.Substring(0, maxLength);

        Toggles = Toggles.Where(t =>
            prototypeManager.HasIndex<ConsentTogglePrototype>(t.Key)
            && t.Value == "on"
        ).ToDictionary();
    }
}

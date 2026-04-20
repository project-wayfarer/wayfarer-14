using Content.Shared.Traits;
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Preferences.Loadouts.Effects;

public sealed partial class TraitLoadoutEffect : LoadoutEffect
{
    [DataField(required: true)]
    public ProtoId<TraitPrototype> Trait;

    public override bool Validate(HumanoidCharacterProfile profile, RoleLoadout loadout, ICommonSession? session, IDependencyCollection collection,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        if (profile.TraitPreferences.Contains(Trait))
        {
            reason = null;
            return true;
        }
        var protoMan = collection.Resolve<IPrototypeManager>();
        var traitName = Loc.GetString(protoMan.Index(Trait).Name);
        reason = FormattedMessage.FromUnformatted(Loc.GetString("loadout-trait-restriction", ("trait", traitName)));
        return false;
    }
}

// This is like the species check for loadout items, except for traits.

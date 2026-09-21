using Content.Shared._DV.Traits;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private void OnTraitsSelectionChanged(HashSet<ProtoId<TraitPrototype>> traits)
    {
        if (Profile is null)
            return;

        foreach (var existingTrait in Profile.TraitPreferences)
        {
            // Without this, using the traits tab clears whatever the Outlaw Objectives tab set.
            if (!IsHidden(existingTrait))
                Profile = Profile.WithoutTraitPreference(existingTrait, _prototypeManager);
        }

        foreach (var trait in traits)
            Profile = Profile.WithTraitPreference(trait.Id, _prototypeManager);

        SetDirty();
    }

    private void UpdateTraitsSelection()
    {
        if (Profile is null)
        {
            Traits.SetSelectedTraits(new HashSet<ProtoId<TraitPrototype>>());
            return;
        }

        var selectedTraits = new HashSet<ProtoId<TraitPrototype>>(Profile.TraitPreferences.Count);
        foreach (var traitId in Profile.TraitPreferences)
        {
            if (_prototypeManager.TryIndex(traitId, out TraitPrototype? trait) && !trait.Hidden)
                selectedTraits.Add(new ProtoId<TraitPrototype>(traitId));
        }

        Traits.SetSelectedTraits(selectedTraits);
        Traits.UpdateConditions(Profile);
    }

    private bool IsHidden(ProtoId<TraitPrototype> traitId)
    {
        return _prototypeManager.TryIndex(traitId, out var trait) && trait.Hidden;
    }
}

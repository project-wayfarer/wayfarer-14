using Content.Client._WF.Lobby.UI;
using Content.Shared._DV.Traits;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private OutlawObjectivesTab _outlawObjectives = default!;

    // Called after the description tab has been added, so this tab ends up at the right-hand end.
    private void InitializeOutlawObjectives()
    {
        _outlawObjectives = new OutlawObjectivesTab();
        TabContainer.AddChild(_outlawObjectives);
        TabContainer.SetTabTitle(TabContainer.ChildCount - 1, Loc.GetString("humanoid-profile-editor-outlaw-objectives-tab"));

        _outlawObjectives.OnObjectiveToggled += (trait, chosen) =>
        {
            if (Profile is null)
                return;

            Profile = chosen
                ? Profile.WithTraitPreference(trait, _prototypeManager)
                : Profile.WithoutTraitPreference(trait, _prototypeManager);

            SetDirty();
        };
    }

    private void UpdateOutlawObjectivesSelection()
    {
        _outlawObjectives.SetSelected(Profile?.TraitPreferences ?? new HashSet<ProtoId<TraitPrototype>>());
    }
}

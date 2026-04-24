using Content.Client._WF.CommunityGoals.UI;
using Content.Shared._WF.CommunityGoals.BUI;
using Content.Shared._WF.CommunityGoals.Events;
using Robust.Client.UserInterface;

namespace Content.Client._WF.CommunityGoals;

public sealed class CommunityGoalConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CommunityGoalConsoleWindow? _window;

    public CommunityGoalConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CommunityGoalConsoleWindow>();
        _window.Title = Loc.GetString("community-goal-console-title");
        _window.OnCommit += () => SendMessage(new CommunityGoalCommitMessage());
        _window.OnClearStaging += () => SendMessage(new CommunityGoalClearStagingMessage());
        _window.OnContributeToRequirement += reqId => SendMessage(new CommunityGoalContributeToRequirementMessage(reqId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CommunityGoalConsoleState castState)
            return;

        _window?.UpdateState(castState);
    }
}

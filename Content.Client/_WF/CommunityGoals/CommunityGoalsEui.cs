using Content.Client._WF.CommunityGoals.UI;
using Content.Client.Eui;
using Content.Shared._WF.CommunityGoals;
using Content.Shared.Eui;

namespace Content.Client._WF.CommunityGoals;

public sealed class CommunityGoalsEui : BaseEui
{
    private readonly CommunityGoalsWindow _window;

    public CommunityGoalsEui()
    {
        _window = new CommunityGoalsWindow();

        _window.OnClose += () => SendMessage(new CloseEuiMessage());

        _window.OnCreateGoal += (title, desc, start, end) =>
            SendMessage(new CreateCommunityGoalMessage(title, desc, start, end));

        _window.OnUpdateGoal += (id, title, desc, start, end, active) =>
            SendMessage(new UpdateCommunityGoalMessage(id, title, desc, start, end, active));

        _window.OnDeleteGoal += id =>
            SendMessage(new DeleteCommunityGoalMessage(id));

        _window.OnAddRequirement += (goalId, protoId, displayName, amount) =>
            SendMessage(new AddCommunityGoalRequirementMessage(goalId, protoId, displayName, amount));

        _window.OnRemoveRequirement += requirementId =>
            SendMessage(new RemoveCommunityGoalRequirementMessage(requirementId));

        _window.OnUpdateRequirement += (requirementId, amount) =>
            SendMessage(new UpdateCommunityGoalRequirementMessage(requirementId, amount));
    }

    public override void Opened()
    {
        base.Opened();
        _window.OpenCentered();
    }

    public override void Closed()
    {
        base.Closed();
        _window.Close();
    }

    public override void HandleState(EuiStateBase state)
    {
        if (state is not CommunityGoalsEuiState cast)
            return;

        _window.HandleState(cast);
    }
}

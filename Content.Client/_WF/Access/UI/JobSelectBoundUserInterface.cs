// Note: This is mostly a barebones copy of AgentIDCardBoundUserInterface.cs
// There's nothing special about it. It hijacks into most of the agent ID's methods to work, and is incredibly hacky.

using Content.Shared.Access.Systems;
using Content.Shared.StatusIcon;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._WF.Access.UI;

public sealed class JobSelectBoundUserInterface : BoundUserInterface
{
    private JobSelectWindow? _window;

    public JobSelectBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<JobSelectWindow>();

        _window.OnJobChanged += OnJobChanged;
        _window.OnJobIconChanged += OnJobIconChanged;
    }

    private void OnJobChanged(string newJob)
    {
        SendMessage(new AgentIDCardJobChangedMessage(newJob));
    }

    public void OnJobIconChanged(ProtoId<JobIconPrototype> newJobIconId)
    {
        SendMessage(new AgentIDCardJobIconChangedMessage(newJobIconId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (_window == null || state is not AgentIDCardBoundUserInterfaceState cast)
            return;

        _window.SetCurrentJob(cast.CurrentJob);
        _window.SetAllowedIcons(cast.CurrentJobIconId);
    }
}

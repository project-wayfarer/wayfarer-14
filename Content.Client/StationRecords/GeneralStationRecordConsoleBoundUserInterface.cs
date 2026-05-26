using Content.Shared.StationRecords;
using Robust.Client.UserInterface;
using Content.Shared._NF.StationRecords; // Frontier
using Content.Shared.Roles; // Frontier
using Robust.Shared.Prototypes; // Frontier
using Content.Shared._WF.StationRecords.Components; // Wayfarer
using Content.Shared.Containers.ItemSlots; // Wayfarer

namespace Content.Client.StationRecords;

public sealed class GeneralStationRecordConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private GeneralStationRecordConsoleWindow? _window = default!;

    public GeneralStationRecordConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<GeneralStationRecordConsoleWindow>();
        _window.OnKeySelected += key =>
            SendMessage(new SelectStationRecord(key));
        _window.OnFiltersChanged += (type, filterValue) =>
            SendMessage(new SetStationRecordFilter(type, filterValue));
        _window.OnJobAdd += OnJobsAdd; // Frontier: job modification buttons
        _window.OnJobSubtract += OnJobsSubtract; // Frontier: job modification buttons
        _window.OnDeleted += id => SendMessage(new DeleteStationRecord(id));
        _window.OnAdvertisementChanged += OnAdvertisementChanged; // Frontier: job modification buttons
        // Wayfarer: Register Crew tab buttons
        _window.PrivilegedIdButton.OnPressed += _ => SendMessage(new ItemSlotButtonPressedEvent(RegisterCrewConsoleComponent.PrivilegedIdSlotId));
        _window.TargetIdButton.OnPressed += _ => SendMessage(new ItemSlotButtonPressedEvent(RegisterCrewConsoleComponent.TargetIdSlotId));
        _window.OnRegisterCrew += text => SendMessage(new RegisterCrewMessage(text));
        _window.OnRemoveCrew += id => SendMessage(new RemoveCrewMessage(id));
        // End Wayfarer
    }

    // Frontier: job modification buttons, ship advertisements
    private void OnJobsAdd(ProtoId<JobPrototype> job)
    {
        SendMessage(new AdjustStationJobMsg(job, 1));
    }
    private void OnJobsSubtract(ProtoId<JobPrototype> job)
    {
        SendMessage(new AdjustStationJobMsg(job, -1));
    }
    private void OnAdvertisementChanged(string text)
    {
        SendMessage(new SetStationAdvertisementMsg(text));
    }
    // End Frontier
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not GeneralStationRecordConsoleState cast)
            return;

        _window?.UpdateState(cast);
    }
}

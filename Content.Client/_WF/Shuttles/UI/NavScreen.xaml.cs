using Content.Shared.Shuttles.BUIStates;
using Content.Client._WF.Shuttles;
using Content.Shared._WF.Shuttles.Components;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Shuttles.UI;

public sealed partial class NavScreen
{
    public event Action<NetEntity?>? OnAutopilotToggled;

    private bool _autopilotEnabled;

    private Button _lootScannerToggle = default!;

    private void WfInitialize()
    {
        // Autopilot button only enables - clicking it when already enabled does nothing
        AutopilotButton.OnPressed += _ => EnableAutopilot();

        var shipValues = _entManager.System<ShipValueSystem>();
        _lootScannerToggle = new Button
        {
            Text = Loc.GetString("loot-scanner-toggle"),
            TextAlign = Label.AlignMode.Center,
            ToggleMode = true,
            Pressed = shipValues.Enabled,
            Margin = new Thickness(0, 0, 0, 5),
            Visible = false,
        };
        _lootScannerToggle.OnToggled += args => shipValues.Enabled = args.Pressed;
        NavRadarSettingsButton.Parent!.AddChild(_lootScannerToggle);
        _lootScannerToggle.SetPositionInParent(NavRadarSettingsButton.GetPositionInParent() + 1);
    }

    private void EnableAutopilot()
    {
        // Only send enable request if not already enabled
        if (_autopilotEnabled)
        {
            // Keep the button visually pressed since autopilot is still active
            AutopilotButton.Pressed = true;
            return;
        }

        _entManager.TryGetNetEntity(_shuttleEntity, out var shuttle);
        OnAutopilotToggled?.Invoke(shuttle);
    }

    private void WfUpdateState(NavInterfaceState state)
    {
        _autopilotEnabled = state.AutopilotEnabled;

        _lootScannerToggle.Visible = _entManager.HasComponent<LootScannerComponent>(_consoleEntity);

        // Always show autopilot button, but disable it if no autopilot server is available
        AutopilotButton.Visible = true;
        AutopilotButton.Disabled = !state.HasAutopilotServer;

        // Update button pressed state
        AutopilotButton.Pressed = state.AutopilotEnabled;

        // When autopilot is active, unpress the dampener mode buttons
        if (state.AutopilotEnabled)
        {
            DampenerOff.Pressed = false;
            DampenerOn.Pressed = false;
            AnchorOn.Pressed = false;
        }
    }
}

using Content.Shared._WF.CartridgeLoader.Cartridges;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;

namespace Content.Client._WF.CartridgeLoader.Cartridges;

public sealed partial class CriticalImplantTrackerUiFragment : BoxContainer
{
    public event Action? OnRefreshPressed;

    private readonly Button _refreshButton;
    private readonly BoxContainer _patientList;

    public CriticalImplantTrackerUiFragment()
    {
        RobustXamlLoader.Load(this);

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;

        _refreshButton = FindControl<Button>("RefreshButton")!;
        _patientList = FindControl<BoxContainer>("PatientList")!;

        _refreshButton.OnPressed += _ => OnRefreshPressed?.Invoke();
    }

    public void UpdateState(List<CriticalPatientData> patients)
    {
        _patientList.DisposeAllChildren();
        _patientList.RemoveAllChildren();

        if (patients.Count == 0)
        {
            var noDataLabel = new Label
            {
                Text = "No critical patients with implants found.",
                HorizontalAlignment = HAlignment.Center,
                Margin = new Thickness(0, 20, 0, 0)
            };
            _patientList.AddChild(noDataLabel);
            return;
        }

        foreach (var patient in patients)
        {
            // Patient container
            var patientContainer = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(0, 0, 0, 12)
            };

            // Header with name and status badge
            var headerContainer = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 2)
            };

            // Status badge
            var statusBadge = new Label
            {
                Text = patient.IsDead ? "DEAD" : "CRIT",
                StyleClasses = { "LabelSmall" },
                Margin = new Thickness(0, 0, 8, 0)
            };
            if (patient.IsDead)
            {
                statusBadge.FontColorOverride = Color.Red;
            }
            else
            {
                statusBadge.FontColorOverride = Color.Orange;
            }
            headerContainer.AddChild(statusBadge);

            // Patient name
            var nameLabel = new Label
            {
                Text = patient.Name,
                StyleClasses = { "LabelHeading" }
            };
            headerContainer.AddChild(nameLabel);
            patientContainer.AddChild(headerContainer);

            // Patient info (species and coordinates)
            var infoLabel = new Label
            {
                Text = $"{patient.Species} - Location: {patient.Coordinates}",
                Margin = new Thickness(0, 2, 0, 2)
            };
            patientContainer.AddChild(infoLabel);

            // Time since crit
            var timeLabel = new Label
            {
                Text = $"Time since {(patient.IsDead ? "death" : "crit")}: {patient.TimeSinceCrit}",
                Margin = new Thickness(0, 0, 0, 4)
            };
            patientContainer.AddChild(timeLabel);

            // Implants list
            var implantsList = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Margin = new Thickness(16, 4, 0, 0)
            };

            foreach (var implant in patient.Implants)
            {
                var implantLabel = new Label
                {
                    Text = $"• {implant}",
                    Margin = new Thickness(0, 2, 0, 2)
                };
                implantsList.AddChild(implantLabel);
            }

            patientContainer.AddChild(implantsList);

            // Separator line
            var separator = new PanelContainer
            {
                MinHeight = 1,
                HorizontalExpand = true,
                StyleClasses = { "LowDivider" }
            };
            patientContainer.AddChild(separator);

            _patientList.AddChild(patientContainer);
        }
    }
}

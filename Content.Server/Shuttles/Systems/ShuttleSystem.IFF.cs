using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    private void InitializeIFF()
    {
        SubscribeLocalEvent<IFFComponent, ComponentStartup>(OnIFFStartup); // Wayfarer
        SubscribeLocalEvent<IFFConsoleComponent, AnchorStateChangedEvent>(OnIFFConsoleAnchor);
        SubscribeLocalEvent<IFFConsoleComponent, IFFShowIFFMessage>(OnIFFShow);
        SubscribeLocalEvent<IFFConsoleComponent, IFFShowVesselMessage>(OnIFFShowVessel);
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
    }

    // Wayfarer start: Fix the IFF console not accurately reflecting its grid's flags when spawned on the grid
    private void OnIFFStartup(EntityUid uid, IFFComponent component, ComponentStartup args)
    {
        UpdateIFFInterfaces(uid, component);
    }
    // End Wayfarer

    private void OnGridSplit(ref GridSplitEvent ev)
    {
        var splitMass = _cfg.GetCVar(CCVars.HideSplitGridsUnder);

        if (splitMass < 0)
            return;

        foreach (var grid in ev.NewGrids)
        {
            if (!_physicsQuery.TryGetComponent(grid, out var physics) ||
                physics.Mass > splitMass)
            {
                continue;
            }

            AddIFFFlag(grid, IFFFlags.HideLabel);
        }
    }

    private void OnIFFShow(EntityUid uid, IFFConsoleComponent component, IFFShowIFFMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null ||
            (component.AllowedFlags & IFFFlags.HideLabel) == 0x0)
        {
            return;
        }

        if (!args.Show)
        {
            AddIFFFlag(xform.GridUid.Value, IFFFlags.HideLabel);
        }
        else
        {
            RemoveIFFFlag(xform.GridUid.Value, IFFFlags.HideLabel);
        }
    }

    private void OnIFFShowVessel(EntityUid uid, IFFConsoleComponent component, IFFShowVesselMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null ||
            (component.AllowedFlags & IFFFlags.Hide) == 0x0)
        {
            return;
        }

        if (!args.Show)
        {
            AddIFFFlag(xform.GridUid.Value, IFFFlags.Hide);
        }
        else
        {
            RemoveIFFFlag(xform.GridUid.Value, IFFFlags.Hide);
        }
    }

    // Wayfarer start: Enable IFF if the console is detached
    private void OnIFFConsoleAnchor(EntityUid uid, IFFConsoleComponent component, ref AnchorStateChangedEvent args)
    {
        // If there's no IFF component, disable the UI
        if (!TryComp(uid, out TransformComponent? xform) || !TryComp(xform.GridUid, out IFFComponent? iff))
        {
            DisableUi(uid, component);
            return;
        }

        // If we're unanchoring, also disable the UI
        if (!args.Anchored)
        {
            // Force IFF on
            RemoveIFFFlag(xform.GridUid.Value, IFFFlags.HideLabel, iff);

            DisableUi(uid, component);
            return;
        }

        // If we're anchoring, update the UI with the IFF flags
        _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
        {
            AllowedFlags = component.AllowedFlags,
            Flags = iff.Flags,
        });
    }

    private void DisableUi(EntityUid uid, IFFConsoleComponent component)
    {
        _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
        {
            AllowedFlags = component.AllowedFlags,
            Flags = IFFFlags.None,
        });
    }
    // End Wayfarer

    protected override void UpdateIFFInterfaces(EntityUid gridUid, IFFComponent component)
    {
        base.UpdateIFFInterfaces(gridUid, component);

        var query = AllEntityQuery<IFFConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = comp.AllowedFlags,
                Flags = component.Flags,
            });
        }
    }
}

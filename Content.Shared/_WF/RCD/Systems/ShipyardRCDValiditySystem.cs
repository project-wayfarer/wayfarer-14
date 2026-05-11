using Content.Shared._NF.GridAccess;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.RCD.Components;
using Content.Shared.RCD.Systems;
using Robust.Shared.Map.Components;
using Content.Shared.Tiles;

namespace Content.Shared._WF.RCD.Systems;

/// <summary>
/// Wayfarer: Moved check logic to its own section
/// </summary>
public sealed class ShipyardRCDValiditySystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public bool CheckRestrictions(
        EntityUid used,
        EntityUid user,
        EntityUid gridUid)
    {
        // Frontier: Prevent RCD usage on protected grids.
        if (TryComp<ProtectedGridComponent>(gridUid, out var protectedGrid)
            && protectedGrid.PreventRCDUse)
        {
            _popup.PopupClient(
                Loc.GetString("rcd-component-use-blocked"),
                used,
                user);

            return false;
        }

        // Frontier: Grid access restrictions.
        if (TryComp<GridAccessComponent>(used, out var gridAccess))
        {
            if (!GridAccessSystem.IsAuthorized(
                    gridUid,
                    gridAccess,
                    out var popupMessage))
            {
                if (popupMessage != null)
                {
                    _popup.PopupClient(
                        Loc.GetString("rcd-component-" + popupMessage),
                        used,
                        user);
                }

                return false;
            }
        }

        return true;
    }
}
using Content.Client.UserInterface.Fragments;
using Content.Shared._WF.Corporations;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._WF.Corporations;

public sealed partial class CorporationUi : UIFragment
{
    private CorporationUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new CorporationUiFragment();

        _fragment.OnRefresh += () =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationRefreshMessage()));

        _fragment.OnNavigate += view =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationNavigateMessage { View = view }));

        _fragment.OnCreate += (name, description, privacy) =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationCreateMessage
            {
                Name = name,
                Description = description,
                Privacy = privacy,
            }));

        _fragment.OnJoin += corpId =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationJoinMessage { CorporationId = corpId }));

        _fragment.OnLeave += () =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationLeaveMessage()));

        _fragment.OnDisband += () =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationDisbandMessage()));

        _fragment.OnEditDescription += description =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationEditDescriptionMessage { Description = description }));

        _fragment.OnSetPrivacy += privacy =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationSetPrivacyMessage { Privacy = privacy }));

        _fragment.OnSendInvite += characterName =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationSendInviteMessage { CharacterName = characterName }));

        _fragment.OnRespondInvite += (corpId, accept) =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationRespondInviteMessage
            {
                CorporationId = corpId,
                Accept = accept,
            }));

        _fragment.OnKick += targetUserId =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationKickMessage { TargetUserId = targetUserId }));

        _fragment.OnChangeRank += (targetUserId, newRank) =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationChangeRankMessage
            {
                TargetUserId = targetUserId,
                NewRank = newRank,
            }));

        _fragment.OnPurchaseStation += stationName =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationPurchaseStationMessage
            {
                StationName = stationName,
            }));

        _fragment.OnToggleStationVisibility += () =>
            userInterface.SendMessage(new CartridgeUiMessage(new CorporationToggleStationVisibilityMessage()));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        switch (state)
        {
            case CorporationListUiState listState:
                _fragment?.ShowListState(listState);
                break;
            case CorporationInviteUiState inviteState:
                _fragment?.ShowInviteState(inviteState);
                break;
        }
    }
}

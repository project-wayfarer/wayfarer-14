using System.Linq;
using System.Threading.Tasks;
using Content.Server.CartridgeLoader;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Server._NF.Bank;
using Content.Shared._WF.CCVar;
using Content.Shared._WF.Corporations;
using Content.Shared.CartridgeLoader;
using Content.Shared.Chat;
using Content.Shared.StationRecords;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server._WF.Corporations;

/// <summary>
/// Manages player corporations: creation, membership, invites, ranks, and database persistence.
/// </summary>
public sealed class CorporationCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly BankSystem _bank = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly CorporationStationSystem _stations = default!;

    private ISawmill _log = default!;

    // Lifecycle

    public override void Initialize()
    {
        base.Initialize();
        _log = _logManager.GetSawmill("wf.corporations");

        SubscribeLocalEvent<CorporationCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<CorporationCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
    }

    // Event handlers

    private async void OnUiReady(EntityUid uid, CorporationCartridgeComponent comp, CartridgeUiReadyEvent args)
    {
        await UpdateListUi(uid, args.Loader, comp);
    }

    private async void OnUiMessage(EntityUid uid, CorporationCartridgeComponent comp, CartridgeMessageEvent args)
    {
        var loader = GetEntity(args.LoaderUid);
        var actor = args.Actor;

        if (!TryGetActorUserId(actor, out var userId))
            return;

        switch (args)
        {
            case CorporationRefreshMessage:
                await UpdateListUi(uid, loader, comp);
                break;

            case CorporationNavigateMessage nav:
                await HandleNavigate(uid, loader, comp, actor, userId, nav.View);
                break;

            case CorporationCreateMessage create:
                await HandleCreate(uid, loader, comp, actor, userId, create);
                break;

            case CorporationJoinMessage join:
                await HandleJoin(uid, loader, comp, actor, userId, join.CorporationId);
                break;

            case CorporationLeaveMessage:
                await HandleLeave(uid, loader, comp, actor, userId);
                break;

            case CorporationDisbandMessage:
                await HandleDisband(uid, loader, comp, actor, userId);
                break;

            case CorporationEditDescriptionMessage edit:
                await HandleEditDescription(uid, loader, comp, actor, userId, edit.Description);
                break;

            case CorporationSetPrivacyMessage privacy:
                await HandleSetPrivacy(uid, loader, comp, actor, userId, privacy.Privacy);
                break;

            case CorporationSendInviteMessage invite:
                await HandleSendInvite(uid, loader, comp, actor, userId, invite.CharacterName);
                break;

            case CorporationRespondInviteMessage respond:
                await HandleRespondInvite(uid, loader, comp, actor, userId, respond.CorporationId, respond.Accept);
                break;

            case CorporationKickMessage kick:
                await HandleKick(uid, loader, comp, actor, userId, kick.TargetUserId);
                break;

            case CorporationChangeRankMessage changeRank:
                await HandleChangeRank(uid, loader, comp, actor, userId, changeRank.TargetUserId, changeRank.NewRank);
                break;

            case CorporationPurchaseStationMessage purchaseStation:
                await HandlePurchaseStation(uid, loader, comp, actor, userId, purchaseStation.StationName);
                break;

            case CorporationToggleStationVisibilityMessage:
                await HandleToggleStationVisibility(uid, loader, comp, actor, userId);
                break;
        }
    }

    // Action handlers

    private async Task HandleNavigate(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId, CorporationView view)
    {
        if (view == CorporationView.Invite)
        {
            var characterName = GetCharacterName(actor);
            var myCorp = await GetCorporationForCharacter(userId, characterName);
            var myMember = GetMember(myCorp, userId, characterName);

            if (myCorp == null || myMember == null || (CorporationRank)myMember.Rank < CorporationRank.Recruiter)
            {
                await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
                return;
            }

            var characters = GetStationCharacterNames(myCorp);
            var state = new CorporationInviteUiState { AvailableCharacters = characters };
            _cartridgeLoader.UpdateCartridgeUiState(loader, state);
        }
        else
        {
            await UpdateListUi(uid, loader, comp);
        }
    }

    private async Task HandleCreate(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId, CorporationCreateMessage create)
    {
        var characterName = GetCharacterName(actor);

        // Must not already be in a corp
        if (await GetCorporationForCharacter(userId, characterName) != null)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-already-member");
            return;
        }

        // Validate name
        var name = create.Name.Trim();
        var nameMax = _cfg.GetCVar(WFCCVars.CorporationNameMaxLength);
        if (name.Length == 0 || name.Length > nameMax)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-invalid-name");
            return;
        }

        // Name must be unique — check all corps
        var allCorps = await _db.GetAllCorporations();
        if (allCorps.Any(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
        {
            await UpdateListUi(uid, loader, comp, "corp-error-name-taken");
            return;
        }

        // Validate description
        var description = create.Description.Trim();
        var descMax = _cfg.GetCVar(WFCCVars.CorporationDescriptionMaxLength);
        if (description.Length > descMax)
            description = description[..descMax];

        // Charge the bank account
        var cost = _cfg.GetCVar(WFCCVars.CorporationCreationCost);
        if (!_bank.TryBankWithdraw(actor, cost))
        {
            await UpdateListUi(uid, loader, comp, "corp-error-insufficient-funds");
            return;
        }

        var displayName = MetaData(actor).EntityName;
        await _db.CreateCorporation(name, description, (int)create.Privacy, userId.UserId, displayName);

        _log.Info($"Player {userId} founded corporation '{name}'.");
        await UpdateListUi(uid, loader, comp);
    }

    private async Task HandleJoin(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId, int corpId)
    {
        var characterName = GetCharacterName(actor);

        if (await GetCorporationForCharacter(userId, characterName) != null)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-already-member");
            return;
        }

        var corp = await _db.GetCorporationById(corpId);
        if (corp == null)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-not-found");
            return;
        }

        // Non-public corps require an invite.
        if (corp.Privacy != (int)CorporationPrivacy.Public)
        {
            if (!await _db.HasCorporationInvite(corpId, userId.UserId))
            {
                await UpdateListUi(uid, loader, comp, "corp-error-invite-required");
                return;
            }
        }

        var displayName = MetaData(actor).EntityName;
        await _db.AddCorporationMember(corpId, userId.UserId, displayName, (int)CorporationRank.Member);
        await _db.RemoveCorporationInvite(corpId, userId.UserId);

        _log.Info($"Player {userId} joined corporation '{corp.Name}'.");
        await UpdateListUi(uid, loader, comp);
    }

    private async Task HandleLeave(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId)
    {
        var characterName = GetCharacterName(actor);
        var corp = await GetCorporationForCharacter(userId, characterName);
        var myMember = GetMember(corp, userId, characterName);

        if (corp == null || myMember == null)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-not-in-corp");
            return;
        }

        // Leader cannot leave if other members remain
        if ((CorporationRank)myMember.Rank == CorporationRank.Leader && corp.Members.Count > 1)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-leader-cannot-leave");
            return;
        }

        // If the leader is the only member, disband
        if (corp.Members.Count == 1)
        {
            await _db.DeleteCorporation(corp.Id);
        }
        else
        {
            await _db.RemoveCorporationMember(corp.Id, userId.UserId);
        }

        _log.Info($"Player {userId} left corporation '{corp.Name}'.");
        await UpdateListUi(uid, loader, comp);
    }

    private async Task HandleDisband(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId)
    {
        var characterName = GetCharacterName(actor);
        var corp = await GetCorporationForCharacter(userId, characterName);
        var myMember = GetMember(corp, userId, characterName);

        if (corp == null || myMember == null || (CorporationRank)myMember.Rank != CorporationRank.Leader)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
            return;
        }

        _log.Info($"Player {userId} disbanded corporation '{corp.Name}'.");
        await _db.DeleteCorporation(corp.Id);
        await UpdateListUi(uid, loader, comp);
    }

    private async Task HandleEditDescription(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId, string description)
    {
        var characterName = GetCharacterName(actor);
        var corp = await GetCorporationForCharacter(userId, characterName);
        var myMember = GetMember(corp, userId, characterName);

        if (corp == null || myMember == null || (CorporationRank)myMember.Rank < CorporationRank.Manager)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
            return;
        }

        var descMax = _cfg.GetCVar(WFCCVars.CorporationDescriptionMaxLength);
        description = description.Trim();
        if (description.Length > descMax)
            description = description[..descMax];

        await _db.UpdateCorporationDescription(corp.Id, description);
        await UpdateListUi(uid, loader, comp);
    }

    private async Task HandleSetPrivacy(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId, CorporationPrivacy privacy)
    {
        var characterName = GetCharacterName(actor);
        var corp = await GetCorporationForCharacter(userId, characterName);
        var myMember = GetMember(corp, userId, characterName);

        if (corp == null || myMember == null || (CorporationRank)myMember.Rank < CorporationRank.Manager)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
            return;
        }

        await _db.UpdateCorporationPrivacy(corp.Id, (int)privacy);
        await UpdateListUi(uid, loader, comp);
    }

    private async Task HandleSendInvite(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId, string characterName)
    {
        var actorCharacterName = GetCharacterName(actor);
        var corp = await GetCorporationForCharacter(userId, actorCharacterName);
        var myMember = GetMember(corp, userId, actorCharacterName);

        if (corp == null || myMember == null || (CorporationRank)myMember.Rank < CorporationRank.Recruiter)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
            return;
        }

        // Find the target player by their character name in active sessions
        if (!TryFindPlayerByName(characterName, out var targetUserId))
        {
            var characters = GetStationCharacterNames(corp);
            _cartridgeLoader.UpdateCartridgeUiState(loader, new CorporationInviteUiState
            {
                AvailableCharacters = characters,
                ErrorMessage = "corp-error-player-not-found",
            });
            return;
        }

        // Target must not already be in any corporation
        if (await GetCorporationForCharacter(targetUserId, characterName) != null)
        {
            var characters = GetStationCharacterNames(corp);
            _cartridgeLoader.UpdateCartridgeUiState(loader, new CorporationInviteUiState
            {
                AvailableCharacters = characters,
                ErrorMessage = "corp-error-target-in-corp",
            });
            return;
        }

        // Target must not already have a pending invite to this corp
        if (await _db.HasCorporationInvite(corp.Id, targetUserId.UserId))
        {
            var characters = GetStationCharacterNames(corp);
            _cartridgeLoader.UpdateCartridgeUiState(loader, new CorporationInviteUiState
            {
                AvailableCharacters = characters,
                ErrorMessage = "corp-error-already-invited",
            });
            return;
        }

        await _db.AddCorporationInvite(corp.Id, targetUserId.UserId);
        _log.Info($"Player {userId} invited '{characterName}' ({targetUserId}) to corporation '{corp.Name}'.");

        // Notify the invited player if they are online
        if (_playerManager.TryGetSessionById(targetUserId, out var targetSession))
        {
            var inviteMsg = Loc.GetString("corp-notify-invited", ("corp", corp.Name));
            var inviteWrapped = Loc.GetString("chat-manager-server-wrap-message",
                ("message", FormattedMessage.EscapeText(inviteMsg)));
            _chat.ChatMessageToOne(ChatChannel.Server, inviteMsg, inviteWrapped, EntityUid.Invalid,
                false, targetSession.Channel, colorOverride: Color.FromHex("#FF69B4"));
        }

        await UpdateListUi(uid, loader, comp);
    }

    private async Task HandleRespondInvite(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId, int corpId, bool accept)
    {
        var characterName = GetCharacterName(actor);

        if (!await _db.HasCorporationInvite(corpId, userId.UserId))
        {
            await UpdateListUi(uid, loader, comp, "corp-error-invite-not-found");
            return;
        }

        await _db.RemoveCorporationInvite(corpId, userId.UserId);

        if (accept)
        {
            // Must not already be in a corp
            if (await GetCorporationForCharacter(userId, characterName) != null)
            {
                await UpdateListUi(uid, loader, comp, "corp-error-already-member");
                return;
            }

            var corp = await _db.GetCorporationById(corpId);
            var displayName = MetaData(actor).EntityName;
            await _db.AddCorporationMember(corpId, userId.UserId, displayName, (int)CorporationRank.Member);
            _log.Info($"Player {userId} accepted invite to corporation '{corp?.Name}'.");
        }
        else
        {
            _log.Info($"Player {userId} declined invite to corporation {corpId}.");
        }

        await UpdateListUi(uid, loader, comp);
    }

    private async Task HandleKick(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId, string targetUserIdStr)
    {
        var characterName = GetCharacterName(actor);
        var corp = await GetCorporationForCharacter(userId, characterName);
        var myMember = GetMember(corp, userId, characterName);

        if (corp == null || myMember == null || (CorporationRank)myMember.Rank < CorporationRank.Manager)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
            return;
        }

        if (!Guid.TryParse(targetUserIdStr, out var targetGuid))
        {
            await UpdateListUi(uid, loader, comp, "corp-error-member-not-found");
            return;
        }

        var target = corp.Members.FirstOrDefault(m => m.UserId == targetGuid);
        if (target == null)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-member-not-found");
            return;
        }

        var myRank = (CorporationRank)myMember.Rank;
        var targetRank = (CorporationRank)target.Rank;
        if (targetRank >= myRank)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
            return;
        }

        await _db.RemoveCorporationMember(corp.Id, targetGuid);
        _log.Info($"Player {userId} kicked '{target.DisplayName}' from corporation '{corp.Name}'.");
        await UpdateListUi(uid, loader, comp);
    }

    private async Task HandleChangeRank(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId, string targetUserIdStr, CorporationRank newRank)
    {
        var characterName = GetCharacterName(actor);
        var corp = await GetCorporationForCharacter(userId, characterName);
        var myMember = GetMember(corp, userId, characterName);

        if (corp == null || myMember == null || (CorporationRank)myMember.Rank < CorporationRank.Manager)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
            return;
        }

        if (!Guid.TryParse(targetUserIdStr, out var targetGuid))
        {
            await UpdateListUi(uid, loader, comp, "corp-error-member-not-found");
            return;
        }

        var target = corp.Members.FirstOrDefault(m => m.UserId == targetGuid);
        if (target == null)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-member-not-found");
            return;
        }

        var myRank = (CorporationRank)myMember.Rank;
        var currentTargetRank = (CorporationRank)target.Rank;

        // Cannot change rank of someone at or above your own level
        if (currentTargetRank >= myRank)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
            return;
        }

        // Cannot promote someone to a rank equal to or above your own
        if (newRank >= myRank)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
            return;
        }

        // Cannot demote below Member
        if (newRank < CorporationRank.Member)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-invalid-rank");
            return;
        }

        await _db.UpdateCorporationMemberRank(corp.Id, targetGuid, (int)newRank);
        _log.Info($"Player {userId} changed '{target.DisplayName}' rank to {newRank} in '{corp.Name}'.");
        await UpdateListUi(uid, loader, comp);
    }

    private async Task HandlePurchaseStation(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId, string stationName)
    {
        if (!_cfg.GetCVar(WFCCVars.CorporationStationPurchaseEnabled))
        {
            await UpdateListUi(uid, loader, comp, "corp-error-station-purchase-disabled");
            return;
        }

        var characterName = GetCharacterName(actor);
        var corp = await GetCorporationForCharacter(userId, characterName);
        var myMember = GetMember(corp, userId, characterName);

        if (corp == null || myMember == null || (CorporationRank)myMember.Rank < CorporationRank.Manager)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
            return;
        }

        stationName = stationName.Trim();
        if (string.IsNullOrEmpty(stationName))
        {
            await UpdateListUi(uid, loader, comp, "corp-error-station-name-empty");
            return;
        }

        if (stationName.Length > 40)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-station-name-too-long");
            return;
        }

        var purchased = await _stations.PurchaseStation(corp.Id, stationName);
        if (!purchased)
        {
            // Could be already has station or insufficient funds — check which
            var existing = await _db.GetCorporationStation(corp.Id);
            var errorKey = existing != null ? "corp-error-station-exists" : "corp-error-insufficient-funds";
            await UpdateListUi(uid, loader, comp, errorKey);
            return;
        }

        _log.Info($"Player {userId} purchased station '{stationName}' for corporation '{corp.Name}'.");
        await UpdateListUi(uid, loader, comp);
    }

    private async Task HandleToggleStationVisibility(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        EntityUid actor, NetUserId userId)
    {
        var characterName = GetCharacterName(actor);
        var corp = await GetCorporationForCharacter(userId, characterName);
        var myMember = GetMember(corp, userId, characterName);

        if (corp == null || myMember == null || (CorporationRank)myMember.Rank < CorporationRank.Manager)
        {
            await UpdateListUi(uid, loader, comp, "corp-error-no-permission");
            return;
        }

        _stations.ToggleStationVisibility(corp.Id);
        await UpdateListUi(uid, loader, comp);
    }

    // UI state helpers

    private async Task UpdateListUi(EntityUid uid, EntityUid loader, CorporationCartridgeComponent comp,
        string? errorLocKey = null)
    {
        var session = FindSessionForLoader(loader);
        if (session == null)
            return;

        var characterName = GetCharacterName(session);
        var state = await BuildListState(session.UserId, characterName, errorLocKey);
        _cartridgeLoader.UpdateCartridgeUiState(loader, state);
    }

    private async Task<CorporationListUiState> BuildListState(NetUserId userId, string? characterName, string? errorLocKey = null)
    {
        var myCorp = await GetCorporationForCharacter(userId, characterName);
        var myMember = GetMember(myCorp, userId, characterName);
        var myRank = myMember != null ? (CorporationRank)myMember.Rank : CorporationRank.Member;
        var myStation = myCorp != null ? await _db.GetCorporationStation(myCorp.Id) : null;

        var members = myCorp?.Members.Select(m => new CorporationMemberInfo
        {
            UserId = m.UserId.ToString(),
            DisplayName = m.DisplayName,
            Rank = (CorporationRank)m.Rank,
        }).ToList() ?? new List<CorporationMemberInfo>();

        var allCorps = await _db.GetAllCorporations();

        var publicCorps = allCorps
            .Where(c => c.Privacy != (int)CorporationPrivacy.Unlisted &&
                        (myCorp == null || c.Id != myCorp.Id))
            .Select(c => new CorporationInfo
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Privacy = (CorporationPrivacy)c.Privacy,
                MemberCount = c.Members.Count,
                Balance = c.Balance,
            })
            .ToList();

        var pendingInvites = allCorps
            .Where(c => c.PendingInvites.Any(i => i.InviteeUserId == userId.UserId))
            .Select(c => new CorporationInfo
            {
                Id = c.Id,
                Name = c.Name,
                Description = c.Description,
                Privacy = (CorporationPrivacy)c.Privacy,
                MemberCount = c.Members.Count,
                Balance = c.Balance,
            })
            .ToList();

        return new CorporationListUiState
        {
            MyCorporation = myCorp != null ? new CorporationInfo
            {
                Id = myCorp.Id,
                Name = myCorp.Name,
                Description = myCorp.Description,
                Privacy = (CorporationPrivacy)myCorp.Privacy,
                MemberCount = myCorp.Members.Count,
                Balance = myCorp.Balance,
                HasStation = myStation != null,
                StationName = myStation?.StationName,
                StationVisible = myCorp != null && _stations.IsStationVisible(myCorp.Id),
                StationCoordinates = myCorp != null ? _stations.GetStationCoordinates(myCorp.Id) : null,
                StationUpkeepCost = myCorp != null ? _stations.GetUpkeepCost(myCorp.Id) : null,
            } : null,
            MyRank = myRank,
            Members = members,
            PublicCorporations = publicCorps,
            PendingInvites = pendingInvites,
            ErrorMessage = errorLocKey,
            MyUserId = userId.UserId.ToString(),
            StationPurchaseEnabled = _cfg.GetCVar(WFCCVars.CorporationStationPurchaseEnabled),
        };
    }

    // Data query helpers

    private static WayfarerCorporationMember? GetMember(WayfarerCorporation? corp, NetUserId userId, string? characterName)
    {
        if (corp == null)
            return null;

        return corp.Members.FirstOrDefault(m =>
            m.UserId == userId.UserId &&
            (string.IsNullOrWhiteSpace(characterName) || m.DisplayName == characterName));
    }

    private async Task<WayfarerCorporation?> GetCorporationForCharacter(NetUserId userId, string? characterName)
    {
        if (string.IsNullOrWhiteSpace(characterName))
            return null;

        return await _db.GetCorporationForCharacter(userId.UserId, characterName);
    }

    private bool TryGetActorUserId(EntityUid actor, out NetUserId userId)
    {
        userId = default;
        if (!_playerManager.TryGetSessionByEntity(actor, out var session))
            return false;
        userId = session.UserId;
        return true;
    }

    private string? GetCharacterName(EntityUid actor)
    {
        var name = MetaData(actor).EntityName.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private string? GetCharacterName(ICommonSession session)
    {
        if (session.AttachedEntity is not { } attached)
            return null;

        var name = MetaData(attached).EntityName.Trim();
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// Tries to find a currently-connected player by their character's display name.
    /// </summary>
    private bool TryFindPlayerByName(string characterName, out NetUserId userId)
    {
        userId = default;
        foreach (var session in _playerManager.Sessions)
        {
            if (session.AttachedEntity is not { } entityUid)
                continue;
            if (MetaData(entityUid).EntityName.Equals(characterName, StringComparison.OrdinalIgnoreCase))
            {
                userId = session.UserId;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Returns names of all on-station characters that are not already members of or invited to the given corp.
    /// Used to populate the invite character dropdown.
    /// </summary>
    private List<string> GetStationCharacterNames(WayfarerCorporation corp)
    {
        var memberUserIds = corp.Members.Select(m => m.UserId).ToHashSet();
        var pendingInviteUserIds = corp.PendingInvites.Select(i => i.InviteeUserId).ToHashSet();
        var names = new HashSet<string>();

        var allStations = EntityQueryEnumerator<StationRecordsComponent>();
        while (allStations.MoveNext(out var stationUid, out _))
        {
            var icRecords = _records.GetRecordsOfType<GeneralStationRecord>(stationUid);
            foreach (var (_, record) in icRecords)
            {
                if (string.IsNullOrWhiteSpace(record.Name))
                    continue;

                if (!TryFindPlayerByName(record.Name, out var recordUserId))
                    continue;

                if (memberUserIds.Contains(recordUserId.UserId) ||
                    pendingInviteUserIds.Contains(recordUserId.UserId))
                    continue;

                names.Add(record.Name);
            }
        }

        return names.OrderBy(n => n).ToList();
    }

    /// <summary>
    /// Attempts to find the ICommonSession associated with the loader entity (the PDA holder).
    /// </summary>
    private ICommonSession? FindSessionForLoader(EntityUid loader)
    {
        var parent = Transform(loader).ParentUid;
        while (parent.IsValid())
        {
            if (_playerManager.TryGetSessionByEntity(parent, out var session))
                return session;
            parent = Transform(parent).ParentUid;
        }
        return null;
    }
}

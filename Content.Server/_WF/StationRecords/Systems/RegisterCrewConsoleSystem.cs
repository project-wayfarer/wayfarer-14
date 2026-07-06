using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._WF.StationRecords.Components;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Database;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.StationRecords.Systems;

public sealed class RegisterCrewConsoleSystem : EntitySystem
{
    private static readonly SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_sigh.ogg");
    private static readonly SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly GeneralStationRecordConsoleSystem _generalConsole = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<RegisterCrewConsoleComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<RegisterCrewConsoleComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<RegisterCrewConsoleComponent, EntRemovedFromContainerMessage>(OnSlotChanged);

        Subs.BuiEvents<RegisterCrewConsoleComponent>(GeneralStationRecordConsoleKey.Key, subs =>
        {
            subs.Event<RegisterCrewMessage>(OnRegisterCrew);
            subs.Event<RemoveCrewMessage>(OnRemoveCrew);
        });
    }

    private void OnComponentInit(EntityUid uid, RegisterCrewConsoleComponent component, ComponentInit args)
    {
        _itemSlots.AddItemSlot(uid, RegisterCrewConsoleComponent.TargetIdSlotId, component.TargetIdSlot);
        _itemSlots.AddItemSlot(uid, RegisterCrewConsoleComponent.PrivilegedIdSlotId, component.PrivilegedIdSlot);
    }

    private void OnSlotChanged(EntityUid uid, RegisterCrewConsoleComponent component, ContainerModifiedMessage args)
    {
        if (args.Container.ID == RegisterCrewConsoleComponent.TargetIdSlotId
            || args.Container.ID == RegisterCrewConsoleComponent.PrivilegedIdSlotId)
            _generalConsole.RefreshExternal(uid);
    }

    private void OnRegisterCrew(EntityUid uid, RegisterCrewConsoleComponent component, RegisterCrewMessage args)
    {
        if (component.PrivilegedIdSlot.ContainerSlot?.ContainedEntity is not { Valid: true } privilegedId
            || component.TargetIdSlot.ContainerSlot?.ContainedEntity is not { Valid: true } targetId)
        {
            _popup.PopupEntity(Loc.GetString("register-crew-no-idcard"), args.Actor);
            _audio.PlayPredicted(ErrorSound, uid, null);
            return;
        }

        if (_station.GetOwningStation(uid) is not { } stationUid)
            return;

        if (!IsAuthorized(privilegedId, stationUid))
        {
            _popup.PopupEntity(Loc.GetString("register-crew-not-authorized"), args.Actor);
            _audio.PlayPredicted(ErrorSound, uid, null);
            return;
        }

        var idCard = Comp<IdCardComponent>(targetId);
        var job = _prototype.EnumeratePrototypes<JobPrototype>()
            .FirstOrDefault(j => j.LocalizedName == idCard.LocalizedJobTitle);
        if (job is null)
            return;

        if (!TryComp<StationRecordsComponent>(stationUid, out var stationRecords))
            return;

        var name = !string.IsNullOrWhiteSpace(idCard.FullName) ? idCard.FullName : Name(targetId);
        var profile = HumanoidCharacterProfile.DefaultWithSpecies().WithName(name);

        _records.CreateGeneralRecord(stationUid, targetId, name, profile.Age, profile.Species, profile.Gender, job.ID, null, null, profile, stationRecords);

        // If a custom job title was typed, override the manifest text only.
        if (!string.IsNullOrWhiteSpace(args.CustomJobTitle)
            && _records.GetRecordByName(stationUid, name) is { } recordId)
        {
            var key = new StationRecordKey(recordId, stationUid);
            if (_records.TryGetRecord<GeneralStationRecord>(key, out var record))
            {
                record.JobTitle = args.CustomJobTitle;
                _records.Synchronize(key);
            }
        }

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):actor} registered {ToPrettyString(targetId):target} as crew ({job.ID}) on {ToPrettyString(stationUid):station}.");

        _audio.PlayPredicted(ConfirmSound, uid, null);

        _generalConsole.RefreshExternal(uid);
    }

    private void OnRemoveCrew(EntityUid uid, RegisterCrewConsoleComponent component, RemoveCrewMessage args)
    {
        if (component.PrivilegedIdSlot.ContainerSlot?.ContainedEntity is not { Valid: true } privilegedId)
        {
            _popup.PopupEntity(Loc.GetString("register-crew-no-privileged-id"), args.Actor);
            _audio.PlayPredicted(ErrorSound, uid, null);
            return;
        }

        if (_station.GetOwningStation(uid) is not { } stationUid)
            return;

        if (!IsAuthorized(privilegedId, stationUid))
        {
            _popup.PopupEntity(Loc.GetString("register-crew-not-authorized"), args.Actor);
            _audio.PlayPredicted(ErrorSound, uid, null);
            return;
        }

        var key = new StationRecordKey(args.RecordId, stationUid);
        if (!_records.TryGetRecord<GeneralStationRecord>(key, out var record))
            return;

        if (IsStationOwnerRecord(args.RecordId, stationUid))
        {
            _popup.PopupEntity(Loc.GetString("register-crew-cannot-remove-owner"), args.Actor);
            _audio.PlayPredicted(ErrorSound, uid, null);
            return;
        }

        _records.RemoveRecord(key);

        _adminLogger.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.Actor):actor} removed {record.Name} ({record.JobTitle}) from the crew of {ToPrettyString(stationUid):station}.");

        _audio.PlayPredicted(ConfirmSound, uid, null);
        _generalConsole.RefreshExternal(uid);
    }

    private bool IsStationOwnerRecord(uint recordId, EntityUid stationUid)
    {
        var query = EntityQueryEnumerator<ShuttleDeedComponent, StationRecordKeyStorageComponent>();
        while (query.MoveNext(out _, out var deed, out var keyStorage))
        {
            if (deed.ShuttleUid is not { } deedGrid
                || Deleted(deedGrid)
                || !TryComp<StationMemberComponent>(deedGrid, out var deedMember)
                || deedMember.Station != stationUid)
                continue;
            if (keyStorage.Key is { } key && key.Id == recordId)
                return true;
        }
        return false;
    }

    private bool IsAuthorized(EntityUid privilegedId, EntityUid stationUid)
    {
        if (TryComp<ShuttleDeedComponent>(privilegedId, out var deed)
            && deed.ShuttleUid is { } deedGrid
            && !Deleted(deedGrid)
            && TryComp<StationMemberComponent>(deedGrid, out var deedMember)
            && deedMember.Station == stationUid)
            return true;

        if (!TryComp<StationJobsComponent>(stationUid, out var jobs)
            || !TryComp<AccessComponent>(privilegedId, out var idAccess))
            return false;

        if (jobs.Tags.Any(idAccess.Tags.Contains))
            return true;

        foreach (var group in jobs.Groups)
        {
            if (_prototype.TryIndex(group, out var accessGroup)
                && accessGroup.Tags.Any(idAccess.Tags.Contains))
                return true;
        }

        return false;
    }
}

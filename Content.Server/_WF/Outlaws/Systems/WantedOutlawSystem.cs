using Content.Server.CriminalRecords.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Server.StationRecords.Systems;
using Content.Shared.Clothing;
using Content.Shared.Forensics.Components;
using Content.Shared.GameTicking;
using Content.Shared.Inventory;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Radio;
using Content.Shared.Security;

namespace Content.Server._WF.Outlaws;

[RegisterComponent]
public sealed partial class WantedOutlawComponent : Component;

public sealed class WantedOutlawSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly CriminalRecordsSystem _criminalRecords = default!;
    [Dependency] private readonly RadioSystem _radio = default!;

    [ValidatePrototypeId<RadioChannelPrototype>]
    private const string CgpChannel = "Nfsd";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WantedOutlawComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(EntityUid uid, WantedOutlawComponent component, PlayerSpawnCompleteEvent args)
    {
        if (args.JobId is not { } jobId)
            return;

        _inventory.TryGetSlotEntity(uid, "id", out var idUid);
        TryComp<FingerprintComponent>(uid, out var fingerprint);
        TryComp<DnaComponent>(uid, out var dna);

        if (_records.TryCreateSectorRecord(uid, idUid, args.Profile, jobId, fingerprint?.Fingerprint, dna?.DNA) is not { } key)
            return;

        var reason = args.Profile.Loadouts.TryGetValue(LoadoutSystem.GetJobPrototype(jobId), out var loadout)
                     && !string.IsNullOrWhiteSpace(loadout.CrimeReason)
            ? loadout.CrimeReason
            : Loc.GetString("wf-wanted-outlaw-crime-reason-unspecified");

        _criminalRecords.TryChangeStatus(
            key,
            SecurityStatus.Wanted,
            reason,
            Loc.GetString("wf-wanted-outlaw-initiator"));

        _radio.SendRadioMessage(key.OriginStation, Loc.GetString(
            "wf-wanted-outlaw-arrival-broadcast",
            ("name", args.Profile.Name),
            ("reason", reason)), CgpChannel, uid);
    }
}
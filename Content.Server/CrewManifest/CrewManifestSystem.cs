using System.Linq;
using Content.Server.Access.Components; // Coyote
using Content.Server.Access.Systems; // Coyote
using Content.Server.Administration;
using Content.Server.EUI;
using Content.Server.Station.Systems;
using Content.Server.StationRecords;
using Content.Server.StationRecords.Systems;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.CrewManifest;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Content.Shared.Station.Components;
using Content.Shared.StationRecords;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

using Content.Shared.Medical.SuitSensors; // Coyote
using Content.Shared.SSDIndicator; // Coyote

namespace Content.Server.CrewManifest;

public sealed class CrewManifestSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly StationRecordsSystem _recordsSystem = default!;
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IdCardSystem _idCardSystem = default!; // Coyote

    /// <summary>
    ///     Cached crew manifest entries. The alternative is to outright
    ///     rebuild the crew manifest every time the state is requested:
    ///     this is inefficient.
    /// </summary>
    private readonly Dictionary<EntityUid, CrewManifestEntries> _cachedEntries = new();

    private readonly Dictionary<EntityUid, Dictionary<ICommonSession, CrewManifestEui>> _openEuis = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<AfterGeneralRecordCreatedEvent>(AfterGeneralRecordCreated);
        SubscribeLocalEvent<RecordModifiedEvent>(OnRecordModified);
        SubscribeLocalEvent<RecordRemovedEvent>(OnRecordRemoved);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeNetworkEvent<RequestCrewManifestMessage>(OnRequestCrewManifest);

        SubscribeLocalEvent<CrewManifestViewerComponent, BoundUIClosedEvent>(OnBoundUiClose);
        SubscribeLocalEvent<CrewManifestViewerComponent, CrewManifestOpenUiMessage>(OpenEuiFromBui);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        foreach (var (_, euis) in _openEuis)
        {
            foreach (var (_, eui) in euis)
            {
                eui.Close();
            }
        }

        _openEuis.Clear();
        _cachedEntries.Clear();
    }

    private void OnRequestCrewManifest(RequestCrewManifestMessage message, EntitySessionEventArgs args)
    {
        if (args.SenderSession is not { } sessionCast
            || !_configManager.GetCVar(CCVars.CrewManifestWithoutEntity))
        {
            return;
        }

        OpenEui(GetEntity(message.Id), sessionCast);
    }

    // Not a big fan of this one. Rebuilds the crew manifest every time
    // somebody spawns in, meaning that at round start, it rebuilds the crew manifest
    // wrt the amount of players readied up.
    private void AfterGeneralRecordCreated(AfterGeneralRecordCreatedEvent ev)
    {
        // Coyote: NOP, we build on open
        // BuildCrewManifest();
        // UpdateEuis(ev.Key.OriginStation);
        // End Coyote
    }

    private void OnRecordModified(RecordModifiedEvent ev)
    {
        // Coyote: NOP, we build on open
        // BuildCrewManifest();
        // UpdateEuis(ev.Key.OriginStation);
        // End Coyote
    }

    private void OnRecordRemoved(RecordRemovedEvent ev)
    {
        // Coyote: NOP, we build on open
        // BuildCrewManifest();
        // UpdateEuis(ev.Key.OriginStation);
        // End Coyote
    }

    private void OnBoundUiClose(EntityUid uid, CrewManifestViewerComponent component, BoundUIClosedEvent ev)
    {
        if (!Equals(ev.UiKey, component.OwnerKey))
            return;

        var owningStation = _stationSystem.GetOwningStation(uid);
        if (owningStation == null || !TryComp(ev.Actor, out ActorComponent? actorComp))
        {
            return;
        }

        CloseEui(owningStation.Value, actorComp.PlayerSession, uid);
    }

    /// <summary>
    ///     Gets the crew manifest for a given station, along with the name of the station.
    /// </summary>
    /// <returns>The name and crew manifest entries (unordered) of the station.</returns>
    public CrewManifestEntries GetCrewManifest() // Coyote: remove args, remove name
    {
        return BuildCrewManifest(); // Coyote
    }

    private void UpdateEuis(EntityUid station)
    {
        if (_openEuis.TryGetValue(station, out var euis))
        {
            foreach (var eui in euis.Values)
            {
                eui.StateDirty();
            }
        }
    }

    private void OpenEuiFromBui(EntityUid uid, CrewManifestViewerComponent component, CrewManifestOpenUiMessage msg)
    {
        if (!msg.UiKey.Equals(component.OwnerKey))
        {
            Log.Error(
                "{User} tried to open crew manifest from wrong UI: {Key}. Correct owned is {ExpectedKey}",
                msg.Actor, msg.UiKey, component.OwnerKey);
            return;
        }

        var owningStation = _stationSystem.GetOwningStation(uid);
        if (owningStation == null || !TryComp(msg.Actor, out ActorComponent? actorComp))
        {
            return;
        }

        if (!_configManager.GetCVar(CCVars.CrewManifestUnsecure) && component.Unsecure)
        {
            return;
        }

        OpenEui(owningStation.Value, actorComp.PlayerSession, uid);
    }

    /// <summary>
    ///     Opens a crew manifest EUI for a given player.
    /// </summary>
    /// <param name="station">Station that we're displaying the crew manifest for.</param>
    /// <param name="session">The player's session.</param>
    /// <param name="owner">If this EUI should be 'owned' by an entity.</param>
    public void OpenEui(EntityUid station, ICommonSession session, EntityUid? owner = null)
    {
        if (!HasComp<StationRecordsComponent>(station))
        {
            return;
        }

        if (!_openEuis.TryGetValue(station, out var euis))
        {
            euis = new();
            _openEuis.Add(station, euis);
        }

        if (euis.ContainsKey(session))
        {
            return;
        }

        var eui = new CrewManifestEui(station, owner, this);
        euis.Add(session, eui);

        _euiManager.OpenEui(eui, session);
        eui.StateDirty();
    }

    /// <summary>
    ///     Closes an EUI for a given player.
    /// </summary>
    /// <param name="station">Station that we're displaying the crew manifest for.</param>
    /// <param name="session">The player's session.</param>
    /// <param name="owner">The owner of this EUI, if there was one.</param>
    public void CloseEui(EntityUid station, ICommonSession session, EntityUid? owner = null)
    {
        if (!HasComp<StationRecordsComponent>(station))
        {
            return;
        }

        if (!_openEuis.TryGetValue(station, out var euis)
            || !euis.TryGetValue(session, out var eui))
        {
            return;
        }

        if (eui.Owner == owner)
        {
            euis.Remove(session);
            eui.Close();
        }

        if (euis.Count == 0)
        {
            _openEuis.Remove(station);
        }
    }

    /// <summary>
    ///     Builds the crew manifest for a station. Stores it in the cache afterwards.
    /// </summary>
    private CrewManifestEntries BuildCrewManifest()
    {
        var sensors = EntityQueryEnumerator<SuitSensorComponent>(); // Coyote

        var entries = new CrewManifestEntries();
        var entriesSort = new List<(JobPrototype? job, CrewManifestEntry entry)>();

        // Coyote start
        while (sensors.MoveNext(out var uid, out var sensor))
        {
            if (sensor.User == null) // Wayfarer: Moved SSD check to allow showing SSD characters in a separate section
            {
                continue;
            }

            var name = Loc.GetString("suit-sensor-component-unknown-name");
            var jobTitle = Loc.GetString("suit-sensor-component-unknown-job");

            if (!_idCardSystem.TryFindIdCard(sensor.User.Value, out var card))
                continue;

            if (card.Comp.FullName != null)
                name = card.Comp.FullName;

            if (card.Comp.LocalizedJobTitle != null)
                jobTitle = card.Comp.LocalizedJobTitle;

            if (!TryComp<PresetIdCardComponent>(card, out var preset))
                continue;

            var jobName = preset.JobName!.Value; // Wayfarer
            if (TryComp<SSDIndicatorComponent>(sensor.User, out var ssd) && ssd.IsSSD) // Wayfarer: Group SSD players separately
                jobName = "Inactive";


            var entry = new CrewManifestEntry(name, jobTitle, card.Comp.JobIcon, jobName); // Wayfarer: preset.JobName!.Value < jobName

            entriesSort.Add((null, entry));
        }
        // End Coyote

        entriesSort.Sort((a, b) =>
        {
            var cmp = JobUIComparer.Instance.Compare(a.job, b.job);
            if (cmp != 0)
                return cmp;

            return string.Compare(a.entry.Name, b.entry.Name, StringComparison.CurrentCultureIgnoreCase);
        });

        entries.Entries = entriesSort.Select(x => x.entry).ToArray();
        // _cachedEntries[station] = entries; // Coyote: causes problems
        return entries; // Coyote
    }
}

[AdminCommand(AdminFlags.Admin)]
public sealed class CrewManifestCommand : LocalizedEntityCommands
{
    [Dependency] private readonly CrewManifestSystem _manifestSystem = default!;

    public override string Command => "crewmanifest";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1)
        {
            shell.WriteLine(Loc.GetString($"shell-need-exactly-one-argument"));
            return;
        }

        if (!NetEntity.TryParse(args[0], out var uidNet) || !EntityManager.TryGetEntity(uidNet, out var uid))
        {
            shell.WriteLine(Loc.GetString($"shell-argument-station-id-invalid", ("index", args[0])));
            return;
        }

        if (shell.Player is not { } session)
        {
            shell.WriteLine(Loc.GetString($"shell-cannot-run-command-from-server"));
            return;
        }

        _manifestSystem.OpenEui(uid.Value, session);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        if (args.Length != 1)
            return CompletionResult.Empty;

        var stations = new List<CompletionOption>();
        var query = EntityManager.EntityQueryEnumerator<StationDataComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var meta = EntityManager.GetComponent<MetaDataComponent>(uid);
            stations.Add(new CompletionOption(uid.ToString(), meta.EntityName));
        }

        return CompletionResult.FromHintOptions(stations, null);
    }
}

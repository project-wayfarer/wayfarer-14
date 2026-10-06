using Content.Server._WF.Shuttles.Components;
using Content.Server.Cargo.Systems;
using Content.Shared._NF.Shipyard.Components;
using Content.Shared._WF.Shuttles.Events;
using Content.Shared.Shuttles.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;
using Content.Shared._WF.Shuttles.Components;
using System.Linq;
using Content.Shared.Materials;
using Content.Shared.Mobs.Components;
using Content.Shared.Whitelist;
using Robust.Shared.Containers;
using Content.Shared.Inventory;
using Content.Shared.Contraband;
using Content.Shared.Store;
using Content.Server.Cargo.Components;
using Content.Shared.Mobs.Systems;
using System.Numerics;
using Robust.Shared.Map;

namespace Content.Server._WF.Shuttles.Systems;

public sealed class ShipValueSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly EntProtoId SettingsSource = "ComputerShuttleAntag";
    private static readonly ProtoId<CurrencyPrototype> Doubloon = "Doubloon";

    private const int ChildrenPerTick = 25;

    private LootScannerComponent? _settings;

    private readonly Queue<EntityUid> _pending = new();
    private bool _sweeping;

    // The ships each open scanner can see on its nav, and so the ones it is sent values for.
    private readonly Dictionary<EntityUid, List<EntityUid>> _scannerGrids = new();
    private List<Entity<MapGridComponent>> _found = new();

    private EntityUid? _walkGrid;
    private readonly List<EntityUid> _walkChildren = new();
    private int _walkIndex;
    private double _walkTotal;
    private int _walkDc;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleDeedComponent, ComponentStartup>(OnDeedStartup);
        SubscribeLocalEvent<LootScannerComponent, BoundUIOpenedEvent>(OnScannerOpened);
        SubscribeLocalEvent<LootScannerComponent, BoundUIClosedEvent>(OnScannerClosed);
        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => _settings = null);
    }

    private TimeSpan NextTick => _timing.TickPeriod * 2;

    private LootScannerComponent Settings =>
        _settings ??= _proto.Index(SettingsSource).TryGetComponent<LootScannerComponent>(out var settings, _factory)
            ? settings
            : throw new InvalidOperationException($"{SettingsSource} has no LootScanner component");

    private void OnDeedStartup(EntityUid uid, ShuttleDeedComponent deed, ComponentStartup args)
    {
        if (HasComp<MapGridComponent>(uid))
            EnsureComp<ShipValueComponent>(uid);
    }

    private void OnScannerOpened(EntityUid uid, LootScannerComponent scanner, BoundUIOpenedEvent args)
    {
        if (!args.UiKey.Equals(ShuttleConsoleUiKey.Key))
            return;

        var grids = GridsInRange(uid);
        _scannerGrids[uid] = grids;

        RaiseNetworkEvent(BuildValues(grids), args.Actor);
        StartSweep();
    }

    private void OnScannerClosed(EntityUid uid, LootScannerComponent scanner, BoundUIClosedEvent args)
    {
        if (!args.UiKey.Equals(ShuttleConsoleUiKey.Key))
            return;

        RaiseNetworkEvent(new LootScannerValuesEvent(), args.Actor);
    }

    /// <summary>
    /// Ships are only priced while someone has a scanner open, and only the ships in range of the NAV screen.
    /// </summary>
    private void StartSweep()
    {
        if (_sweeping)
            return;

        _sweeping = true;
        _scannerGrids.Clear();

        var queued = new HashSet<EntityUid>();
        var scanners = EntityQueryEnumerator<LootScannerComponent>();
        while (scanners.MoveNext(out var uid, out _))
        {
            if (!_ui.IsUiOpen(uid, ShuttleConsoleUiKey.Key))
                continue;

            var grids = GridsInRange(uid);
            _scannerGrids[uid] = grids;

            foreach (var grid in grids)
            {
                if (queued.Add(grid))
                    _pending.Enqueue(grid);
            }
        }

        TryStartNext();
    }

    private void EndSweep()
    {
        if (!HasViewers())
        {
            _sweeping = false;
            return;
        }

        SendToViewers();
        Timer.Spawn(Settings.UpdateInterval, NextSweep);
    }

    /// <summary>
    /// The ships with a deed inside the NAV range.
    /// </summary>
    private List<EntityUid> GridsInRange(EntityUid scanner)
    {
        var grids = new List<EntityUid>();

        if (!TryComp<RadarConsoleComponent>(scanner, out var radar))
            return grids;

        var position = _transform.GetMapCoordinates(scanner);
        var range = new Vector2(radar.MaxRange);

        _found.Clear();
        _mapManager.FindGridsIntersecting(position.MapId, new Box2(position.Position - range, position.Position + range), ref _found, approx: true, includeMap: false);

        foreach (var grid in _found)
        {
            if (HasComp<ShipValueComponent>(grid))
                grids.Add(grid);
        }

        return grids;
    }

    private void NextSweep()
    {
        _sweeping = false;
        StartSweep();
    }

    private bool HasViewers()
    {
        var scanners = EntityQueryEnumerator<LootScannerComponent>();
        while (scanners.MoveNext(out var uid, out _))
        {
            if (_ui.IsUiOpen(uid, ShuttleConsoleUiKey.Key))
                return true;
        }

        return false;
    }

    private void TryStartNext()
    {
        if (_walkGrid != null)
            return;

        if (!HasViewers())
            _pending.Clear();

        while (_pending.TryDequeue(out var uid))
        {
            if (TerminatingOrDeleted(uid) || !HasComp<ShipValueComponent>(uid))
                continue;

            _walkGrid = uid;
            _walkIndex = 0;
            _walkTotal = 0;
            _walkDc = 0;

            _walkChildren.Clear();
            var children = Transform(uid).ChildEnumerator;
            while (children.MoveNext(out var child))
                _walkChildren.Add(child);

            Step();
            return;
        }

        EndSweep();
    }

    /// <summary>
    /// The ship's value is the sell price of the loot directly on the grid. Items are priced 25 per server tick.
    /// </summary>
    private void Step()
    {
        if (_walkGrid is not { } grid || !TryComp<ShipValueComponent>(grid, out var value))
        {
            Abandon();
            return;
        }

        var end = Math.Min(_walkIndex + ChildrenPerTick, _walkChildren.Count);
        for (; _walkIndex < end; _walkIndex++)
        {
            var child = _walkChildren[_walkIndex];

            if (!TerminatingOrDeleted(child))
                _walkTotal += LootValue(child);
        }

        if (_walkIndex < _walkChildren.Count)
        {
            Timer.Spawn(NextTick, Step);
            return;
        }

        value.Shown = (long) _walkTotal;
        value.Dc = _walkDc;

        Abandon();
    }

    private void Abandon()
    {
        _walkGrid = null;
        _walkChildren.Clear();

        if (_pending.Count > 0)
            Timer.Spawn(NextTick, TryStartNext);
        else
            EndSweep();
    }

    private void SendToViewers()
    {
        foreach (var (scanner, grids) in _scannerGrids)
        {
            var filter = Filter.Empty().FromEntities(_ui.GetActors(scanner, ShuttleConsoleUiKey.Key).ToArray());
            if (filter.Count > 0)
                RaiseNetworkEvent(BuildValues(grids), filter);
        }
    }

    private LootScannerValuesEvent BuildValues(List<EntityUid> grids)
    {
        var ev = new LootScannerValuesEvent();

        foreach (var uid in grids)
        {
            if (!TryComp<ShipValueComponent>(uid, out var value))
                continue;

            if (value.Shown >= 1000)
                ev.Values[GetNetEntity(uid)] = value.Shown;

            if (value.Dc > 0)
                ev.DcValues[GetNetEntity(uid)] = value.Dc;
        }

        return ev;
    }

    private double LootValue(EntityUid uid)
    {
        var price = 0.0;

        if (TryComp<ContrabandComponent>(uid, out var contraband) &&
            contraband.TurnInValues.TryGetValue(Doubloon, out var dc) &&
            !_mobState.IsDead(uid) &&
            !HasComp<CargoSellBlacklistComponent>(uid))
            _walkDc += dc;

        if (HasComp<MobStateComponent>(uid))
        {
            foreach (var item in _inventory.GetHandOrInventoryEntities(uid))
                price += LootValue(item);

            return price;
        }

        var loot = _whitelist.IsValid(Settings.Loot, uid);

        if (loot)
        {
            price += _pricing.GetPrice(uid, false);
        }
        // Same sum as MaterialStorageSystem's price handler, without the machine's own price.
        else if (TryComp<MaterialStorageComponent>(uid, out var storage))
        {
            foreach (var (material, amount) in storage.Storage)
            {
                if (_proto.TryIndex<MaterialPrototype>(material, out var proto))
                    price += proto.Price * amount;
            }
        }

        if (TryComp<ContainerManagerComponent>(uid, out var containers))
        {
            foreach (var (id, container) in containers.Containers)
            {
                if (!loot && Settings.IgnoredContainers.Contains(id))
                    continue;

                foreach (var contained in container.ContainedEntities)
                    price += LootValue(contained);
            }
        }

        return price;
    }
}

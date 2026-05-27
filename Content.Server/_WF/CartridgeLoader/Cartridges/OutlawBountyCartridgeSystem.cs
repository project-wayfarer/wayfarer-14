using Content.Server._NF.Pirate.Components;
using Content.Server._NF.SectorServices;
using Content.Server.CartridgeLoader;
using Content.Shared._NF.Pirate;
using Content.Shared._WF.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;

namespace Content.Server._WF.CartridgeLoader.Cartridges;

[RegisterComponent]
public sealed partial class OutlawBountyCartridgeComponent : Component;

/// <summary>
/// Wayfarer: raised when the sector pirate bounty database changes, so cartridges
/// can refresh their UI state.
/// </summary>
public sealed class SectorPirateBountyDatabaseUpdatedEvent : EntityEventArgs;

public sealed class OutlawBountyCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly SectorServiceSystem _sectorService = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<OutlawBountyCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<SectorPirateBountyDatabaseUpdatedEvent>(OnDatabaseUpdated);
    }

    private void OnUiReady(Entity<OutlawBountyCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        if (BuildState() is { } state)
            _cartridgeLoader.UpdateCartridgeUiState(args.Loader, state);
    }

    private void OnDatabaseUpdated(SectorPirateBountyDatabaseUpdatedEvent ev)
    {
        if (BuildState() is not { } state)
            return;

        var query = EntityQueryEnumerator<OutlawBountyCartridgeComponent, CartridgeComponent>();
        while (query.MoveNext(out _, out _, out var cartridge))
        {
            if (cartridge.LoaderUid is { } loader)
                _cartridgeLoader.UpdateCartridgeUiState(loader, state);
        }
    }

    private OutlawBountyUiState? BuildState()
    {
        if (!TryComp<SectorPirateBountyDatabaseComponent>(_sectorService.GetServiceEntity(), out var db))
            return null;
        return new OutlawBountyUiState(new List<PirateBountyData>(db.Bounties));
    }
}
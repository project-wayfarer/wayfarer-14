using Content.Server.CartridgeLoader;
using Content.Server.Ghost.Components;
using Content.Server.Station.Systems;
using Content.Shared._WF.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PDA;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._WF.CartridgeLoader.Cartridges;

public sealed class CriticalImplantTrackerCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CriticalImplantTrackerCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
        SubscribeLocalEvent<CriticalImplantTrackerCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
    }

    private void OnUiMessage(EntityUid uid, CriticalImplantTrackerCartridgeComponent component, CartridgeMessageEvent args)
    {
        if (args is CriticalImplantTrackerRefreshMessage)
        {
            UpdateUiState(uid, GetEntity(args.LoaderUid), component);
        }
    }

    private void OnUiReady(EntityUid uid, CriticalImplantTrackerCartridgeComponent component, CartridgeUiReadyEvent args)
    {
        UpdateUiState(uid, args.Loader, component);
    }

    private void UpdateUiState(EntityUid uid, EntityUid loaderUid, CriticalImplantTrackerCartridgeComponent? component)
    {
        if (!Resolve(uid, ref component))
            return;

        var patients = new List<CriticalPatientData>();

        // Query all entities with MobStateComponent
        var query = AllEntityQuery<MobStateComponent>();
        while (query.MoveNext(out var mobUid, out var mobState))
        {
            var isCritical = _mobStateSystem.IsCritical(mobUid, mobState);
            var isDead = _mobStateSystem.IsDead(mobUid, mobState);
            
            // Only consider entities in critical or dead condition
            if (!isCritical && !isDead)
                continue;
            
            // For dead entities, check if they have PDA and ID card
            if (isDead)
            {
                var hasPda = _inventorySystem.TryGetSlotEntity(mobUid, "id", out var idSlot) && 
                             TryComp<PdaComponent>(idSlot, out var pda) && 
                             pda.ContainedId != null;
                
                if (!hasPda)
                    continue;
            }

            // Get the entity's name
            var name = MetaData(mobUid).EntityName;

            // Get global coordinates
            var xform = Transform(mobUid);
            var globalPos = xform.MapPosition;
            var coordinates = $"({globalPos.X:F0}, {globalPos.Y:F0})";

            // Get species
            var species = "Unknown";
            if (TryComp<HumanoidAppearanceComponent>(mobUid, out var humanoid))
            {
                species = humanoid.Species.ToString();
            }

            // Calculate time since entering crit/death
            // For dead entities, check if they have a ghost component with time of death
            var timeSinceCrit = "Unknown";
            if (isDead && TryComp<GhostComponent>(mobUid, out var ghost))
            {
                var elapsedTime = _gameTiming.CurTime - ghost.TimeOfDeath;
                var totalSeconds = (int)elapsedTime.TotalSeconds;
                var minutes = totalSeconds / 60;
                var seconds = totalSeconds % 60;
                timeSinceCrit = minutes > 0 ? $"{minutes}m {seconds}s" : $"{seconds}s";
            }

            // Find all implants on this entity
            var implants = new List<string>();
            if (_containerSystem.TryGetContainer(mobUid, ImplanterComponent.ImplantSlotId, out var implantContainer))
            {
                foreach (var implant in implantContainer.ContainedEntities)
                {
                    if (HasComp<SubdermalImplantComponent>(implant))
                    {
                        var implantName = MetaData(implant).EntityName;
                        implants.Add(implantName);
                    }
                }
            }

            // Only add patients who have implants
            if (implants.Count > 0)
            {
                patients.Add(new CriticalPatientData(name, implants, coordinates, species, timeSinceCrit, isDead));
            }
        }

        var state = new CriticalImplantTrackerUiState(patients);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }
}

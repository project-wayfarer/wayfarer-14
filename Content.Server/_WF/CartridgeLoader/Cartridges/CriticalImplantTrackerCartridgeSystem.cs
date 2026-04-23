using Content.Server.CartridgeLoader;
using Content.Server.Station.Systems;
using Content.Shared._WF.CartridgeLoader.Cartridges;
using Content.Shared.CartridgeLoader;
using Content.Shared.Ghost;
using Content.Shared.Humanoid;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PDA;
using Content.Shared.Trigger.Components.Conditions;
using Content.Shared.Trigger.Components.Triggers;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._WF.CartridgeLoader.Cartridges;

public sealed class CriticalImplantTrackerCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedContainerSystem _containerSystem = default!;

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
            var timeSinceCrit = "Active";
            if (isDead)
            {
                TimeSpan? timeOfDeath = null;

                // Try to get time of death from mind first
                if (_mindSystem.TryGetMind(mobUid, out var mindId, out var mind) && mind.TimeOfDeath.HasValue)
                {
                    timeOfDeath = mind.TimeOfDeath.Value;
                }
                // Fall back to ghost component if available
                else if (TryComp<GhostComponent>(mobUid, out var ghost))
                {
                    timeOfDeath = ghost.TimeOfDeath;
                }

                if (timeOfDeath.HasValue)
                {
                    var elapsedTime = _gameTiming.CurTime - timeOfDeath.Value;
                    var totalSeconds = (int)elapsedTime.TotalSeconds;
                    var minutes = totalSeconds / 60;
                    var seconds = totalSeconds % 60;
                    timeSinceCrit = minutes > 0 ? $"{minutes}m {seconds}s" : $"{seconds}s";
                }
                else
                {
                    timeSinceCrit = "Unknown";
                }
            }

            // Check if the entity has a medAlert beacon that is enabled
            var hasActiveBeacon = false;
            if (_containerSystem.TryGetContainer(mobUid, ImplanterComponent.ImplantSlotId, out var implantContainer))
            {
                foreach (var implant in implantContainer.ContainedEntities)
                {
                    // Has a mob-state trigger AND is either not togglable or currently toggled on
                    if (TryComp<TriggerOnMobstateChangeComponent>(implant, out _) &&
                        (!TryComp<ToggleTriggerConditionComponent>(implant, out var toggle) || toggle.Enabled))
                    {
                        hasActiveBeacon = true;
                        break;
                    }
                }
            }

            // Only add patients who have an active medAlert beacon
            if (!hasActiveBeacon)
                continue;

            // Add all critical/dead patients with active beacons
            patients.Add(new CriticalPatientData(name, coordinates, species, timeSinceCrit, isDead));
        }

        var state = new CriticalImplantTrackerUiState(patients);
        _cartridgeLoader.UpdateCartridgeUiState(loaderUid, state);
    }
}

using Content.Shared._WF.Silicons.Borgs;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.Containers;

namespace Content.Server._WF.Silicons.Borgs;

public sealed class WFBorgInventorySlotSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<WFBorgInventorySlotModuleComponent, EntGotInsertedIntoContainerMessage>(OnModuleInserted);
        SubscribeLocalEvent<WFBorgInventorySlotModuleComponent, EntGotRemovedFromContainerMessage>(OnModuleRemoved);
        SubscribeLocalEvent<BorgChassisComponent, InventoryTemplateUpdated>(OnTemplateUpdated);
    }

    private void OnModuleInserted(Entity<WFBorgInventorySlotModuleComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (!TryComp<BorgChassisComponent>(args.Container.Owner, out var chassis) || args.Container != chassis.ModuleContainer)
            return;

        Extend(args.Container.Owner, ent.Comp);
    }

    private void OnModuleRemoved(Entity<WFBorgInventorySlotModuleComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (TerminatingOrDeleted(args.Container.Owner))
            return;

        if (!TryComp<BorgChassisComponent>(args.Container.Owner, out var chassis) || args.Container != chassis.ModuleContainer)
            return;

        if (!TryComp<InventoryComponent>(args.Container.Owner, out var inv))
            return;

        foreach (var (baseTemplate, extendedTemplate) in ent.Comp.TemplateMap)
        {
            if (inv.TemplateId == extendedTemplate)
            {
                _inventory.SetTemplateId((args.Container.Owner, inv), baseTemplate);
                return;
            }
        }
    }

    private void OnTemplateUpdated(Entity<BorgChassisComponent> ent, ref InventoryTemplateUpdated args)
    {
        if (ent.Comp.ModuleContainer is null)
            return;

        foreach (var module in ent.Comp.ModuleContainer.ContainedEntities)
        {
            if (TryComp<WFBorgInventorySlotModuleComponent>(module, out var comp))
            {
                Extend(ent.Owner, comp);
                return;
            }
        }
    }

    private void Extend(EntityUid borg, WFBorgInventorySlotModuleComponent comp)
    {
        if (!TryComp<InventoryComponent>(borg, out var inv))
            return;

        if (comp.TemplateMap.TryGetValue(inv.TemplateId, out var extended))
            _inventory.SetTemplateId((borg, inv), extended);
    }
}

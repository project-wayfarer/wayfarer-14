using Content.Shared.Inventory;
using Robust.Shared.Prototypes;

namespace Content.Shared._WF.Silicons.Borgs;

[RegisterComponent]
public sealed partial class WFBorgInventorySlotModuleComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<InventoryTemplatePrototype>, ProtoId<InventoryTemplatePrototype>> TemplateMap = new();
}

using Content.Shared.Whitelist;

namespace Content.Shared._WF.Shuttles.Components;

[RegisterComponent]
public sealed partial class LootScannerComponent : Component
{
    [DataField(required: true)]
    public TimeSpan UpdateInterval;

    [DataField(required: true)]
    public EntityWhitelist Loot = default!;

    [DataField(required: true)]
    public HashSet<string> IgnoredContainers = default!;
}

using Content.Shared._NF.Shipyard.Components;

namespace Content.Server._NF.Shipyard.Systems;

public sealed partial class ShipyardSystem
{
    // Marked as bought with a voucher so the console pays nothing for it.
    public void CopyDeed(Entity<ShuttleDeedComponent> source, EntityUid target)
    {
        var deed = EnsureComp<ShuttleDeedComponent>(target);
        AssignShuttleDeedProperties((target, deed), source.Comp.ShuttleUid, GetFullName(source.Comp), source.Comp.ShuttleOwner, true);
    }
}

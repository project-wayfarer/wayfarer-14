// Wayfarer: IPC EMP shock system
using Content.Server.Electrocution;
using Content.Server.Emp;
using Content.Shared._EinsteinEngines.Silicon.Components;

namespace Content.Server._EinsteinEngines.Silicon.EmpShock;

/// <summary>
/// Wayfarer: This system handles IPCs getting shocked when struck by an EMP,
/// similar to how humans get shocked when touching an electrified door.
/// </summary>
public sealed class SiliconEmpShockSystem : EntitySystem
{
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconComponent, EmpPulseEvent>(OnEmpPulse);
    }

    private void OnEmpPulse(EntityUid uid, SiliconComponent component, ref EmpPulseEvent args)
    {
        // Wayfarer: Apply a short shock effect when the IPC is hit by EMP
        // Similar to touching an electrified door
        // Damage: 5 shock damage (relatively minor)
        // Duration: 3 seconds of stun/jitter
        _electrocution.TryDoElectrocution(uid, null, 5, TimeSpan.FromSeconds(3), true);
    }
}
// End Wayfarer

using Content.Shared.CartridgeLoader;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.CartridgeLoader.Cartridges;

[Serializable, NetSerializable]
public sealed class CriticalImplantTrackerUiState : BoundUserInterfaceState
{
    public List<CriticalPatientData> Patients { get; }

    public CriticalImplantTrackerUiState(List<CriticalPatientData> patients)
    {
        Patients = patients;
    }
}

[Serializable, NetSerializable]
public sealed class CriticalPatientData
{
    public string Name { get; }
    public List<string> Implants { get; }
    public string Coordinates { get; }
    public string Species { get; }
    public string TimeSinceCrit { get; }
    public bool IsDead { get; }

    public CriticalPatientData(string name, List<string> implants, string coordinates, string species, string timeSinceCrit, bool isDead)
    {
        Name = name;
        Implants = implants;
        Coordinates = coordinates;
        Species = species;
        TimeSinceCrit = timeSinceCrit;
        IsDead = isDead;
    }
}

[Serializable, NetSerializable]
public sealed class CriticalImplantTrackerRefreshMessage : CartridgeMessageEvent
{
}

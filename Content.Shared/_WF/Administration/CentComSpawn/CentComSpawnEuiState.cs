using Content.Shared.Eui;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.Administration.CentComSpawn;

public static class CentComSpawnJob
{
    public static readonly ProtoId<JobPrototype> Id = "CentralCommandOfficial";
}

[Serializable, NetSerializable]
public sealed class CentComSpawnEuiState(HumanoidCharacterProfile profile) : EuiStateBase
{
    public HumanoidCharacterProfile Profile { get; } = profile;
}

[Serializable, NetSerializable]
public sealed class CentComSpawnEuiMsg(RoleLoadout loadout) : EuiMessageBase
{
    public RoleLoadout Loadout { get; } = loadout;
}

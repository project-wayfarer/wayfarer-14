using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;

namespace Content.Shared.Examine;

/// <summary>
/// Event sent from client to server to request character information
/// </summary>
[Serializable, NetSerializable]
public sealed class RequestCharacterInfoEvent : EntityEventArgs
{
    public NetEntity Entity { get; set; }
}

/// <summary>
/// Event sent from server to client with character information
/// </summary>
[Serializable, NetSerializable]
public sealed class CharacterInfoEvent : EntityEventArgs
{
    public NetEntity Entity { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ConsentText { get; set; } = string.Empty;
}

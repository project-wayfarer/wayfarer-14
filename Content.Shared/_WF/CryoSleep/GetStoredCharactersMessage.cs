// Wayfarer: Character resume from cryosleep feature
using Content.Shared.Eui;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.CryoSleep;

/// <summary>
/// Request from client to get the list of stored characters in cryo
/// </summary>
[Serializable, NetSerializable]
public sealed class GetStoredCharactersRequestMessage : EntityEventArgs
{
}

/// <summary>
/// Response from server with list of stored characters
/// </summary>
[Serializable, NetSerializable]
public sealed class GetStoredCharactersResponseMessage : EntityEventArgs
{
    public List<StoredCharacterInfo> Characters { get; set; } = new();
    
    public GetStoredCharactersResponseMessage()
    {
    }
    
    public GetStoredCharactersResponseMessage(List<StoredCharacterInfo> characters)
    {
        Characters = characters;
    }
}

/// <summary>
/// Information about a stored character
/// </summary>
[Serializable, NetSerializable]
public sealed class StoredCharacterInfo
{
    public NetEntity Body { get; set; }
    public NetEntity Cryopod { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public string JobName { get; set; } = string.Empty;
    public string StationName { get; set; } = string.Empty;
    /// <summary>
    /// The character preferences slot index. -1 if unknown.
    /// </summary>
    public int CharacterSlot { get; set; } = -1;
    
    public StoredCharacterInfo()
    {
    }
    
    public StoredCharacterInfo(NetEntity body, NetEntity cryopod, string characterName, string jobName, string stationName, int characterSlot = -1)
    {
        Body = body;
        Cryopod = cryopod;
        CharacterName = characterName;
        JobName = jobName;
        StationName = stationName;
        CharacterSlot = characterSlot;
    }
}

/// <summary>
/// Request from client to resume control of a character
/// </summary>
[Serializable, NetSerializable]
public sealed class ResumeCharacterRequestMessage : EntityEventArgs
{
    public NetEntity Body { get; set; }
    
    public ResumeCharacterRequestMessage()
    {
    }
    
    public ResumeCharacterRequestMessage(NetEntity body)
    {
        Body = body;
    }
}

/// <summary>
/// Request from client to permanently remove a stored cryo character (abandon it).
/// </summary>
[Serializable, NetSerializable]
public sealed class RemoveStoredCharacterRequestMessage : EntityEventArgs
{
    public NetEntity Body { get; set; }

    public RemoveStoredCharacterRequestMessage()
    {
    }

    public RemoveStoredCharacterRequestMessage(NetEntity body)
    {
        Body = body;
    }
}

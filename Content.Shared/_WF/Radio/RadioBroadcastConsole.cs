using Robust.Shared.Audio.Midi;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.Radio;

public static class RadioBroadcastConstants
{
    public const int MaxBroadcastNameLength = 32;

    public const string DefaultPresetId = "ElectricGuitarClean";

    public const int MaxUploadBatchSize = 256;

    public const float LayerAudibleRange = SharedAudioSystem.DefaultSoundRange;

    public const float LayerRangeHysteresis = 2f;
}

[Serializable, NetSerializable]
public sealed class RadioMidiUploadEvent : EntityEventArgs
{
    public NetEntity Console;
    public RobustMidiEvent[] MidiEvents;

    public RadioMidiUploadEvent(NetEntity console, RobustMidiEvent[] midiEvents)
    {
        Console = console;
        MidiEvents = midiEvents;
    }
}

[Serializable, NetSerializable]
public enum RadioBroadcastConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class RadioBroadcastConsoleBoundUIState : BoundUserInterfaceState
{
    public int Channel;
    public bool Broadcasting;
    public string BroadcastName;
    public List<string> Presets;
    public List<RadioPresetEntry> AvailablePresets;

    public RadioBroadcastConsoleBoundUIState(int channel, bool broadcasting, string broadcastName,
        List<string> presets, List<RadioPresetEntry> availablePresets)
    {
        Channel = channel;
        Broadcasting = broadcasting;
        BroadcastName = broadcastName;
        Presets = presets;
        AvailablePresets = availablePresets;
    }
}

// The menu sorts instruments in this family order.
[Serializable, NetSerializable]
public enum RadioPresetCategory : byte
{
    Special,
    Strings,
    Brass,
    Woodwind,
    Keyed,
    TunedPercussion,
}

[Serializable, NetSerializable]
public struct RadioPresetEntry
{
    public string Id;
    public string Name;

    public RadioPresetEntry(string id, string name)
    {
        Id = id;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class RadioBroadcastConsoleSetChannelMessage : BoundUserInterfaceMessage
{
    public int Channel;
    public RadioBroadcastConsoleSetChannelMessage(int channel) { Channel = channel; }
}

[Serializable, NetSerializable]
public sealed class RadioBroadcastConsoleSetNameMessage : BoundUserInterfaceMessage
{
    public string Name;
    public RadioBroadcastConsoleSetNameMessage(string name) { Name = name; }
}

[Serializable, NetSerializable]
public sealed class RadioBroadcastConsoleToggleMessage : BoundUserInterfaceMessage
{
    public bool Broadcasting;
    public RadioBroadcastConsoleToggleMessage(bool broadcasting) { Broadcasting = broadcasting; }
}

[Serializable, NetSerializable]
public sealed class RadioBroadcastConsoleSetPresetsMessage : BoundUserInterfaceMessage
{
    public List<ProtoId<RadioPresetPrototype>> PresetIds;
    public RadioBroadcastConsoleSetPresetsMessage(List<ProtoId<RadioPresetPrototype>> presetIds) { PresetIds = presetIds; }
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class RadioBroadcastConsoleComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Channel = 100;

    [DataField, AutoNetworkedField]
    public bool Broadcasting;

    [DataField]
    public float VoiceRange = 3f;

    [DataField]
    public string BroadcastName = string.Empty;

    [DataField, AutoNetworkedField]
    public List<ProtoId<RadioPresetPrototype>> Presets = new() { "ElectricGuitarClean" };
}

[Prototype]
public sealed partial class RadioPresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name = default!;

    [DataField]
    public bool Passthrough;

    [DataField]
    public bool DrumsOnly;

    [DataField]
    public byte Program;

    [DataField]
    public RadioPresetCategory Category;

    [DataField]
    public int Priority;
}

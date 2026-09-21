using Robust.Shared.Audio;
using Robust.Shared.Audio.Midi;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._WF.Radio;

[Serializable, NetSerializable]
public sealed class RadioMidiRelayEvent : EntityEventArgs
{
    public NetEntity Console;
    public int Channel;
    public RobustMidiEvent[] MidiEvents;

    public RadioMidiRelayEvent(NetEntity console, int channel, RobustMidiEvent[] midiEvents)
    {
        Console = console;
        Channel = channel;
        MidiEvents = midiEvents;
    }
}

[Serializable, NetSerializable]
public enum RadioReceiverUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public enum RadioReceiverVisuals : byte
{
    PoweredOn,
}

[Serializable, NetSerializable]
public struct RadioBroadcastEntry
{
    public int Channel;
    public string Name;

    public RadioBroadcastEntry(int channel, string name)
    {
        Channel = channel;
        Name = name;
    }
}

[Serializable, NetSerializable]
public sealed class RadioReceiverBoundUIState : BoundUserInterfaceState
{
    public int Channel;
    public bool PoweredOn;
    public float Volume;
    public List<RadioBroadcastEntry> OnAir;

    public RadioReceiverBoundUIState(int channel, bool poweredOn, float volume, List<RadioBroadcastEntry> onAir)
    {
        Channel = channel;
        PoweredOn = poweredOn;
        Volume = volume;
        OnAir = onAir;
    }
}

[Serializable, NetSerializable]
public sealed class RadioReceiverSetChannelMessage : BoundUserInterfaceMessage
{
    public int Channel;
    public RadioReceiverSetChannelMessage(int channel) { Channel = channel; }
}

[Serializable, NetSerializable]
public sealed class RadioReceiverTogglePowerMessage : BoundUserInterfaceMessage
{
    public bool PoweredOn;
    public RadioReceiverTogglePowerMessage(bool poweredOn) { PoweredOn = poweredOn; }
}

[Serializable, NetSerializable]
public sealed class RadioReceiverSetVolumeMessage : BoundUserInterfaceMessage
{
    public float Volume;
    public RadioReceiverSetVolumeMessage(float volume) { Volume = volume; }
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class RadioReceiverComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Channel = 100;

    [DataField, AutoNetworkedField]
    public bool PoweredOn;

    [DataField, AutoNetworkedField]
    public float Volume = 1f;

    [DataField]
    public SoundSpecifier? ToggleSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");
}

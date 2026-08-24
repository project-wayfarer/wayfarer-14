using System.Linq;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Server.Speech;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Content.Shared._WF;
using Content.Shared._WF.Radio;
using Content.Server.Instruments;
using Content.Shared.Chat;
using Content.Shared.IdentityManagement;
using Content.Shared.Instruments;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Server.GameStates;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._WF.Radio;

public sealed class RadioBroadcastSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly InstrumentSystem _instruments = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly PvsOverrideSystem _pvsOverride = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;

    private readonly Dictionary<int, EntityUid> _activeChannels = new();

    private readonly HashSet<(string, EntityUid, EntityUid)> _recentlySent = new();

    private List<RadioPresetEntry>? _presetEntries;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioBroadcastConsoleComponent, BeforeActivatableUIOpenEvent>(OnConsoleUiOpen);
        SubscribeLocalEvent<RadioBroadcastConsoleComponent, RadioBroadcastConsoleSetChannelMessage>(OnConsoleSetChannel);
        SubscribeLocalEvent<RadioBroadcastConsoleComponent, RadioBroadcastConsoleSetNameMessage>(OnConsoleSetName);
        SubscribeLocalEvent<RadioBroadcastConsoleComponent, RadioBroadcastConsoleToggleMessage>(OnConsoleToggle);
        SubscribeLocalEvent<RadioBroadcastConsoleComponent, RadioBroadcastConsoleSetPresetsMessage>(OnConsoleSetPresets);
        SubscribeLocalEvent<RadioBroadcastConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<RadioBroadcastConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnConsoleGetAltVerbs);
        SubscribeLocalEvent<RadioBroadcastConsoleComponent, BoundUserInterfaceCheckRangeEvent>(OnConsoleCheckRange,
            after: new[] { typeof(SharedInteractionSystem) });

        SubscribeLocalEvent<RadioReceiverComponent, BeforeActivatableUIOpenEvent>(OnReceiverUiOpen);
        SubscribeLocalEvent<RadioReceiverComponent, RadioReceiverSetChannelMessage>(OnReceiverSetChannel);
        SubscribeLocalEvent<RadioReceiverComponent, RadioReceiverTogglePowerMessage>(OnReceiverTogglePower);
        SubscribeLocalEvent<RadioReceiverComponent, RadioReceiverSetVolumeMessage>(OnReceiverSetVolume);

        SubscribeLocalEvent<RadioBroadcastConsoleComponent, ListenEvent>(OnConsoleListen);
        SubscribeLocalEvent<RadioBroadcastConsoleComponent, ListenAttemptEvent>(OnConsoleListenAttempt);

        SubscribeNetworkEvent<RadioMidiUploadEvent>(OnMidiUpload);

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(OnPrototypesReloaded);
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<RadioPresetPrototype>())
            _presetEntries = null;
    }

    private void OnMidiUpload(RadioMidiUploadEvent ev)
    {
        var consoleUid = GetEntity(ev.Console);
        if (!TryComp<RadioBroadcastConsoleComponent>(consoleUid, out var console))
            return;
        if (!console.Broadcasting)
            return;

        // Only players near a radio tuned to this channel are sent the song.
        var listeners = BuildListenerFilter(console.Channel);
        if (listeners == null)
            return;

        RaiseNetworkEvent(new RadioMidiRelayEvent(ev.Console, console.Channel, ev.MidiEvents), listeners);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        _recentlySent.Clear();
    }

    private Filter? BuildListenerFilter(int channel)
    {
        Filter? filter = null;
        var query = EntityQueryEnumerator<RadioReceiverComponent>();
        while (query.MoveNext(out var uid, out var receiver))
        {
            if (!receiver.PoweredOn || receiver.Channel != channel)
                continue;
            filter ??= Filter.Empty();
            filter.AddPlayersByPvs(uid);
        }
        return filter;
    }

    private void OnConsoleUiOpen(Entity<RadioBroadcastConsoleComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateConsoleUi(ent);
    }

    private void OnConsoleGetAltVerbs(Entity<RadioBroadcastConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var console = ent;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("wf-radio-console-verb-settings"),
            Act = () =>
            {
                UpdateConsoleUi(console);
                _ui.OpenUi(console.Owner, RadioBroadcastConsoleUiKey.Key, user);
            },
        });
    }

    // Keep the menu open when the user walks away, so the broadcast keeps playing.
    private void OnConsoleCheckRange(Entity<RadioBroadcastConsoleComponent> ent, ref BoundUserInterfaceCheckRangeEvent args)
    {
        args.Result = BoundUserInterfaceRangeResult.Pass;
    }

    private void OnConsoleSetChannel(Entity<RadioBroadcastConsoleComponent> ent, ref RadioBroadcastConsoleSetChannelMessage args)
    {
        if (ent.Comp.Broadcasting)
            StopBroadcasting(ent);

        ent.Comp.Channel = args.Channel;
        Dirty(ent);
        UpdateConsoleUi(ent);
    }

    private void OnConsoleSetName(Entity<RadioBroadcastConsoleComponent> ent, ref RadioBroadcastConsoleSetNameMessage args)
    {
        var trimmed = args.Name.Trim();
        if (ent.Comp.BroadcastName == trimmed)
            return;

        ent.Comp.BroadcastName = trimmed;
        Dirty(ent);
        UpdateConsoleUi(ent);

        if (ent.Comp.Broadcasting)
            RefreshReceiverUis();
    }

    private void OnConsoleToggle(Entity<RadioBroadcastConsoleComponent> ent, ref RadioBroadcastConsoleToggleMessage args)
    {
        if (args.Broadcasting)
            TryStartBroadcasting(ent, args.Actor);
        else
            StopBroadcasting(ent);

        UpdateConsoleUi(ent);
    }

    private void OnConsoleSetPresets(Entity<RadioBroadcastConsoleComponent> ent, ref RadioBroadcastConsoleSetPresetsMessage args)
    {
        if (ent.Comp.Presets.SequenceEqual(args.PresetIds))
            return;

        ent.Comp.Presets = args.PresetIds;
        Dirty(ent);
        UpdateConsoleUi(ent);
    }

    private void OnConsoleShutdown(Entity<RadioBroadcastConsoleComponent> ent, ref ComponentShutdown args)
    {
        if (ent.Comp.Broadcasting)
            StopBroadcasting(ent);
    }

    private void TryStartBroadcasting(Entity<RadioBroadcastConsoleComponent> ent, EntityUid actor)
    {
        if (ent.Comp.Broadcasting)
            return;

        if (_activeChannels.TryGetValue(ent.Comp.Channel, out var holder) && holder != ent.Owner)
        {
            _popup.PopupEntity(
                Loc.GetString("wf-radio-console-channel-taken", ("n", ent.Comp.Channel)),
                ent,
                actor);
            return;
        }

        _activeChannels[ent.Comp.Channel] = ent.Owner;
        ent.Comp.Broadcasting = true;
        Dirty(ent);

        _pvsOverride.AddGlobalOverride(ent.Owner);

        if (TryComp<InstrumentComponent>(ent.Owner, out var instrument))
            Dirty(ent.Owner, instrument);

        EnsureComp<ActiveListenerComponent>(ent).Range = ent.Comp.VoiceRange;

        RefreshReceiverUis();
    }

    private void StopBroadcasting(Entity<RadioBroadcastConsoleComponent> ent)
    {
        if (!ent.Comp.Broadcasting)
            return;

        if (_activeChannels.TryGetValue(ent.Comp.Channel, out var holder) && holder == ent.Owner)
            _activeChannels.Remove(ent.Comp.Channel);

        ent.Comp.Broadcasting = false;
        Dirty(ent);

        _pvsOverride.RemoveGlobalOverride(ent.Owner);

        RemCompDeferred<ActiveListenerComponent>(ent);

        // Run the instrument cleanup that was skipped while broadcasting. An open instrument menu cleans up on close.
        if (!TerminatingOrDeleted(ent.Owner) && !_ui.IsUiOpen(ent.Owner, InstrumentUiKey.Key))
            _instruments.Clean(ent.Owner);

        RefreshReceiverUis();
    }

    // A radio must not pick up another radio, or speech echoes forever.
    private void OnConsoleListenAttempt(Entity<RadioBroadcastConsoleComponent> ent, ref ListenAttemptEvent args)
    {
        if (HasComp<RadioReceiverComponent>(args.Source) || HasComp<RadioBroadcastConsoleComponent>(args.Source))
            args.Cancel();
    }

    private void OnConsoleListen(Entity<RadioBroadcastConsoleComponent> ent, ref ListenEvent args)
    {
        if (!ent.Comp.Broadcasting)
            return;

        if (!_recentlySent.Add((args.Message, args.Source, ent.Owner)))
            return;

        var nameEv = new TransformSpeakerNameEvent(args.Source, Name(args.Source));
        RaiseLocalEvent(args.Source, nameEv);

        // Color the bubble with the speaker's chat speech color.
        var color = ColorExtensions.ConsistentRandomSeededColorFromString(Identity.Name(args.Source, EntityManager), 149).ToHex();
        var wrapped = Loc.GetString("wf-radio-speech-bubble",
            ("color", color),
            ("name", FormattedMessage.EscapeText($"[{nameEv.VoiceName}]")),
            ("message", FormattedMessage.EscapeText(args.Message)));

        var query = EntityQueryEnumerator<RadioReceiverComponent>();
        while (query.MoveNext(out var receiverUid, out var receiver))
        {
            if (!receiver.PoweredOn || receiver.Channel != ent.Comp.Channel)
                continue;

            // Show the speech as a bubble over the radio, with no chat-log line.
            _chatManager.ChatMessageToManyFiltered(
                Filter.Pvs(receiverUid),
                ChatChannel.Local,
                args.Message,
                wrapped,
                receiverUid,
                hideChat: true,
                recordReplay: false,
                colorOverride: null);
        }
    }

    private void UpdateConsoleUi(Entity<RadioBroadcastConsoleComponent> ent)
    {
        var presets = _presetEntries ??= BuildPresetEntries();

        var selected = new List<string>(ent.Comp.Presets.Count);
        foreach (var id in ent.Comp.Presets)
            selected.Add(id.Id);

        var state = new RadioBroadcastConsoleBoundUIState(
            ent.Comp.Channel,
            ent.Comp.Broadcasting,
            ent.Comp.BroadcastName,
            selected,
            presets);
        _ui.SetUiState(ent.Owner, RadioBroadcastConsoleUiKey.Key, state);
    }

    private List<RadioPresetEntry> BuildPresetEntries()
    {
        // Sort so the menu groups instruments instead of listing them in load order.
        var prototypes = new List<RadioPresetPrototype>(_protos.EnumeratePrototypes<RadioPresetPrototype>());
        prototypes.Sort(ComparePresets);

        var entries = new List<RadioPresetEntry>(prototypes.Count);
        foreach (var preset in prototypes)
        {
            entries.Add(new RadioPresetEntry(preset.ID, Loc.GetString(preset.Name)));
        }
        return entries;
    }

    private static int ComparePresets(RadioPresetPrototype a, RadioPresetPrototype b)
    {
        var byCategory = a.Category.CompareTo(b.Category);
        if (byCategory != 0)
            return byCategory;

        var byPriority = a.Priority.CompareTo(b.Priority);
        if (byPriority != 0)
            return byPriority;

        return string.CompareOrdinal(a.ID, b.ID);
    }

    private void OnReceiverUiOpen(Entity<RadioReceiverComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        UpdateReceiverUi(ent);
    }

    private void OnReceiverSetChannel(Entity<RadioReceiverComponent> ent, ref RadioReceiverSetChannelMessage args)
    {
        ent.Comp.Channel = args.Channel;
        Dirty(ent);
        UpdateReceiverUi(ent);
    }

    private void OnReceiverTogglePower(Entity<RadioReceiverComponent> ent, ref RadioReceiverTogglePowerMessage args)
    {
        ent.Comp.PoweredOn = args.PoweredOn;
        Dirty(ent);
        _appearance.SetData(ent, RadioReceiverVisuals.PoweredOn, args.PoweredOn);
        _item.SetHeldPrefix(ent, args.PoweredOn ? "on" : "off");
        _audio.PlayPvs(ent.Comp.ToggleSound, ent);
        UpdateReceiverUi(ent);
    }

    private void OnReceiverSetVolume(Entity<RadioReceiverComponent> ent, ref RadioReceiverSetVolumeMessage args)
    {
        if (ent.Comp.Volume.Equals(args.Volume))
            return;

        ent.Comp.Volume = args.Volume;
        Dirty(ent);
        UpdateReceiverUi(ent);
    }

    private void UpdateReceiverUi(Entity<RadioReceiverComponent> ent, List<RadioBroadcastEntry>? onAir = null)
    {
        var state = new RadioReceiverBoundUIState(ent.Comp.Channel, ent.Comp.PoweredOn, ent.Comp.Volume, onAir ?? BuildOnAirList());
        _ui.SetUiState(ent.Owner, RadioReceiverUiKey.Key, state);
    }

    private List<RadioBroadcastEntry> BuildOnAirList()
    {
        var list = new List<RadioBroadcastEntry>(_activeChannels.Count);
        foreach (var (channel, consoleUid) in _activeChannels)
        {
            var name = TryComp<RadioBroadcastConsoleComponent>(consoleUid, out var console)
                && !string.IsNullOrWhiteSpace(console.BroadcastName)
                    ? console.BroadcastName
                    : Name(consoleUid);
            list.Add(new RadioBroadcastEntry(channel, name));
        }
        list.Sort((a, b) => a.Channel.CompareTo(b.Channel));
        return list;
    }

    private void RefreshReceiverUis()
    {
        var onAir = BuildOnAirList();
        var query = EntityQueryEnumerator<RadioReceiverComponent>();
        while (query.MoveNext(out var uid, out var receiver))
        {
            UpdateReceiverUi((uid, receiver), onAir);
        }
    }
}

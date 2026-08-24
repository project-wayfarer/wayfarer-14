using Content.Client.Instruments;
using Content.Shared._WF.Radio;
using Content.Shared.Instruments;
using Robust.Client.Audio.Midi;
using Robust.Client.Player;
using Robust.Shared.Audio.Midi;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._WF.Radio;

public sealed class RadioBroadcastSystem : EntitySystem
{
    [Dependency] private readonly IMidiManager _midiManager = default!;
    [Dependency] private readonly IClientNetManager _netManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    private sealed class UploadHook
    {
        public required IMidiRenderer Renderer;
        public required Action<RobustMidiEvent> Handler;
        public readonly List<RobustMidiEvent> Pending = new();

        public bool HandlerAttached;
        public bool WasBroadcaster;

        public IMidiRenderer? Puppet;
        public Action<RobustMidiEvent> PuppetFeed = null!;
        public bool PuppetFeedAttached;

        public bool InRange;

        public readonly List<IMidiRenderer> Slaves = new();

        public readonly List<string> LastAppliedPresets = new();
    }

    private readonly Dictionary<EntityUid, UploadHook> _uploadHooks = new();

    private sealed class ReceiverRenderer
    {
        public required IMidiRenderer Renderer;
        public required NetEntity Console;
        public required int Channel;
        public bool Anchored;
        public uint EventTickAnchor;
        public uint RendererTickAnchor;

        public readonly List<IMidiRenderer> Slaves = new();

        public readonly List<string> LastAppliedPresets = new();
    }

    private readonly Dictionary<EntityUid, ReceiverRenderer> _receiverRenderers = new();
    private readonly List<EntityUid> _reapBuffer = new();

    // Reused so toggling instruments does not slowly degrade the audio.
    private readonly List<IMidiRenderer> _freeRenderers = new();

    private const float RangeCheckInterval = 0.25f;
    private float _rangeCheckTimer;

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;

        SubscribeLocalEvent<RadioBroadcastConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<RadioBroadcastConsoleComponent, AfterAutoHandleStateEvent>(OnConsoleAfterHandleState);
        SubscribeLocalEvent<RadioReceiverComponent, ComponentShutdown>(OnReceiverShutdown);
        SubscribeNetworkEvent<RadioMidiRelayEvent>(OnRadioMidiRelay);
    }

    private void OnConsoleAfterHandleState(Entity<RadioBroadcastConsoleComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        var presets = ResolvePresets(ent.Comp.Presets);
        if (presets.Count == 0)
            return;

        if (TryComp<InstrumentComponent>(ent.Owner, out var instrument)
            && instrument.IsMidiOpen
            && instrument.Renderer is { Disposed: false } consoleRenderer
            && _uploadHooks.TryGetValue(ent.Owner, out var hook)
            && ReferenceEquals(hook.Renderer, consoleRenderer))
        {
            if (SelectionChanged(hook.LastAppliedPresets, ent.Comp.Presets))
                RebuildLayers(consoleRenderer, hook.Slaves, presets, hook.LastAppliedPresets, subscribeToMaster: true);
        }

        var console = GetNetEntity(ent.Owner);
        foreach (var rr in _receiverRenderers.Values)
        {
            if (rr.Console != console || rr.Renderer.Disposed)
                continue;
            if (!SelectionChanged(rr.LastAppliedPresets, ent.Comp.Presets))
                continue;
            RebuildLayers(rr.Renderer, rr.Slaves, presets, rr.LastAppliedPresets, subscribeToMaster: false,
                seedState: ConsoleSeedState(ent.Owner));
        }
    }

    private List<RadioPresetPrototype> ResolvePresets(List<ProtoId<RadioPresetPrototype>> ids)
    {
        var resolved = new List<RadioPresetPrototype>(ids.Count);
        for (var i = 0; i < ids.Count; i++)
        {
            if (_protos.TryIndex<RadioPresetPrototype>(ids[i], out var preset))
                resolved.Add(preset);
        }

        if (resolved.Count == 0
            && _protos.TryIndex<RadioPresetPrototype>(RadioBroadcastConstants.DefaultPresetId, out var fallback))
        {
            resolved.Add(fallback);
        }

        return resolved;
    }

    private static void ApplyPresetToRenderer(IMidiRenderer renderer, RadioPresetPrototype preset, MidiRendererState? seedState = null)
    {
        if (preset.Passthrough)
        {
            // Order matters here. Resetting first brings the song back with the wrong instruments.
            renderer.DisablePercussionChannel = false;
            renderer.DisableProgramChangeEvent = false;
            renderer.SystemReset();

            for (var i = 0; i < renderer.FilteredChannels.Count; i++)
                renderer.FilteredChannels[i] = false;
        }
        else if (preset.DrumsOnly)
        {
            // Never reset for drums. A reset silences them and nothing brings them back.
            renderer.DisableProgramChangeEvent = false;
            renderer.DisablePercussionChannel = false;

            for (var i = 0; i < renderer.FilteredChannels.Count; i++)
            {
                var filtered = i != RobustMidiEvent.PercussionChannel;
                renderer.FilteredChannels[i] = filtered;
                // Stop any note still sounding from the previous instrument.
                if (filtered)
                    renderer.SendMidiEvent(RobustMidiEvent.AllNotesOff((byte) i, renderer.SequencerTick));
            }

            return;
        }
        else
        {
            renderer.SystemReset();

            for (var i = 0; i < renderer.FilteredChannels.Count; i++)
                renderer.FilteredChannels[i] = false;

            renderer.DisablePercussionChannel = true;
            renderer.DisableProgramChangeEvent = true;
            renderer.MidiBank = 0;
            renderer.MidiProgram = preset.Program;
        }

        // Without this every part of the song plays at the same loudness.
        if (seedState != null)
            SeedRenderer(renderer, seedState.Value);
    }

    private static void SeedRenderer(IMidiRenderer renderer, MidiRendererState state)
    {
        renderer.ApplyState(state);
        renderer.StopAllNotes();
    }

    private void RebuildLayers(IMidiRenderer master, List<IMidiRenderer> slaves, List<RadioPresetPrototype> presets, List<string> tracker, bool subscribeToMaster, MidiRendererState? seedState = null)
    {
        if (presets.Count == 0)
        {
            ReleaseSlaves(slaves, 0);
            tracker.Clear();
            return;
        }

        var seed = seedState ?? master.RendererState;

        var prevMaster = tracker.Count > 0 ? tracker[0] : null;
        var masterPresetId = presets[0].ID;
        if (prevMaster != masterPresetId)
            ApplyPresetToRenderer(master, presets[0], seed);

        var slavesNeeded = presets.Count - 1;

        ReleaseSlaves(slaves, slavesNeeded);

        for (var i = 0; i < slavesNeeded; i++)
        {
            var preset = presets[i + 1];
            var prevSlot = tracker.Count > i + 1 ? tracker[i + 1] : null;

            IMidiRenderer? slave;
            if (i < slaves.Count)
            {
                slave = slaves[i];
                if (prevSlot == preset.ID)
                    continue;

                slave.Master = null;
                slave.SendMidiEvent(RobustMidiEvent.SystemReset(slave.SequencerTick));
            }
            else
            {
                slave = RentRenderer();
                if (slave == null)
                    continue;
                slave.SendMidiEvent(RobustMidiEvent.SystemReset(slave.SequencerTick));
                slaves.Add(slave);
            }

            // The instrument played here before can leave its setting behind, so the new one plays wrong without this reset.
            slave.DisableProgramChangeEvent = false;
            SeedRenderer(slave, seed);

            ApplyPresetToRenderer(slave, preset, seed);

            slave.TrackingEntity = master.TrackingEntity;
            slave.TrackingCoordinates = master.TrackingCoordinates;
            if (subscribeToMaster)
                slave.Master = master;
        }

        tracker.Clear();
        foreach (var preset in presets)
            tracker.Add(preset.ID);
    }

    private IMidiRenderer? RentRenderer()
    {
        while (_freeRenderers.Count > 0)
        {
            var pooled = _freeRenderers[^1];
            _freeRenderers.RemoveAt(_freeRenderers.Count - 1);
            if (!pooled.Disposed)
                return pooled;
        }
        return _midiManager.GetNewRenderer();
    }

    private void ReleaseSlaves(List<IMidiRenderer> slaves, int keep)
    {
        for (var i = slaves.Count - 1; i >= keep; i--)
        {
            var slave = slaves[i];
            if (!slave.Disposed)
            {
                slave.Master = null;
                slave.SystemReset();
                slave.ClearAllEvents();
                for (var c = 0; c < slave.FilteredChannels.Count; c++)
                    slave.FilteredChannels[c] = true;
                _freeRenderers.Add(slave);
            }
            slaves.RemoveAt(i);
        }
    }

    private static bool SelectionChanged(List<string> tracker, List<ProtoId<RadioPresetPrototype>> selection)
    {
        // Picking no instruments still plays a default one, so only treat it as unchanged when that default is already playing.
        if (selection.Count == 0)
            return tracker.Count != 1 || tracker[0] != RadioBroadcastConstants.DefaultPresetId;

        if (tracker.Count != selection.Count)
            return true;
        for (var i = 0; i < selection.Count; i++)
        {
            if (tracker[i] != selection[i].Id)
                return true;
        }
        return false;
    }

    private static bool IsPresetDrifted(IMidiRenderer renderer, RadioPresetPrototype preset)
    {
        if (preset.DrumsOnly)
            return renderer.DisableProgramChangeEvent || !renderer.FilteredChannels[0];
        if (preset.Passthrough)
            return renderer.DisableProgramChangeEvent;
        return !renderer.DisableProgramChangeEvent
            || !renderer.DisablePercussionChannel
            || renderer.MidiProgram != preset.Program
            || renderer.MidiBank != 0;
    }

    private bool PresetSlotsDrifted(IMidiRenderer master, List<IMidiRenderer> slaves, List<string> tracker)
    {
        for (var i = 0; i < tracker.Count; i++)
        {
            if (!_protos.TryIndex<RadioPresetPrototype>(tracker[i], out var preset))
                continue;

            var target = i == 0 ? master : i - 1 < slaves.Count ? slaves[i - 1] : null;
            if (target is { Disposed: false } && IsPresetDrifted(target, preset))
                return true;
        }
        return false;
    }

    private static IMidiRenderer? SlotRenderer(ReceiverRenderer rr, int slot)
    {
        IMidiRenderer? target;
        if (slot == 0)
            target = rr.Renderer;
        else if (slot - 1 < rr.Slaves.Count)
            target = rr.Slaves[slot - 1];
        else
            return null;

        return target.Disposed ? null : target;
    }

    private MidiRendererState? ConsoleSeedState(EntityUid consoleUid)
    {
        return TryComp<InstrumentComponent>(consoleUid, out var instrument)
            && instrument.Renderer is { Disposed: false } renderer
                ? renderer.RendererState
                : null;
    }

    private bool LocalPlayerInRange(EntityUid console, bool layersActive)
    {
        if (_player.LocalEntity is not { } player)
            return false;

        var range = RadioBroadcastConstants.LayerAudibleRange
            + (layersActive ? RadioBroadcastConstants.LayerRangeHysteresis : 0f);
        return _xform.InRange(console, player, range);
    }

    private void UpdatePuppetFeed(UploadHook hook)
    {
        var wantFeed = hook.Puppet != null;
        if (wantFeed && !hook.PuppetFeedAttached)
        {
            hook.Renderer.OnMidiEvent += hook.PuppetFeed;
            hook.PuppetFeedAttached = true;
        }
        else if (!wantFeed && hook.PuppetFeedAttached)
        {
            hook.Renderer.OnMidiEvent -= hook.PuppetFeed;
            hook.PuppetFeedAttached = false;
        }
    }

    private void ReleasePuppet(UploadHook hook)
    {
        if (hook.Puppet is { Disposed: false } puppet)
        {
            puppet.SystemReset();
            puppet.ClearAllEvents();
            for (var c = 0; c < puppet.FilteredChannels.Count; c++)
                puppet.FilteredChannels[c] = true;
            _freeRenderers.Add(puppet);
        }
        hook.Puppet = null;
        ReleaseSlaves(hook.Slaves, 0);
        hook.LastAppliedPresets.Clear();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_gameTiming.IsFirstTimePredicted)
            return;

        _rangeCheckTimer += frameTime;
        var checkRange = _rangeCheckTimer >= RangeCheckInterval;
        if (checkRange)
            _rangeCheckTimer = 0f;

        UpdateBroadcasterUploads(checkRange);
        UpdateReceiverRenderers(checkRange);
    }

    private void UpdateBroadcasterUploads(bool checkRange)
    {
        var query = EntityQueryEnumerator<RadioBroadcastConsoleComponent, InstrumentComponent>();
        while (query.MoveNext(out var consoleUid, out var console, out var instrument))
        {
            var renderer = instrument.Renderer;
            var hasRenderer = renderer != null && !renderer.Disposed;
            var shouldMaintain = hasRenderer;

            if (!shouldMaintain)
            {
                if (_uploadHooks.TryGetValue(consoleUid, out var staleHook))
                {
                    if (staleHook.HandlerAttached)
                    {
                        var stop = new[] { RobustMidiEvent.SystemReset(0) };
                        RaiseNetworkEvent(new RadioMidiUploadEvent(GetNetEntity(consoleUid), stop));
                    }
                    DetachUploadHook(consoleUid, staleHook);
                }
                continue;
            }

            _uploadHooks.TryGetValue(consoleUid, out var hook);

            var refreshRange = checkRange;
            if (hook == null || !ReferenceEquals(hook.Renderer, renderer))
            {
                refreshRange = true;
                if (hook != null)
                    DetachUploadHook(consoleUid, hook);

                hook = new UploadHook
                {
                    Renderer = renderer!,
                    Handler = null!,
                };
                hook.WasBroadcaster = instrument.IsMidiOpen;
                hook.Handler = ev => hook.Pending.Add(ev);
                hook.PuppetFeed = ev =>
                {
                    if (hook.Puppet is { Disposed: false } puppet)
                        puppet.ScheduleMidiEvent(ev, 0, false);
                    foreach (var slave in hook.Slaves)
                    {
                        if (!slave.Disposed)
                            slave.ScheduleMidiEvent(ev, 0, false);
                    }
                };

                if (instrument.IsMidiOpen)
                    renderer!.SendMidiEvent(RobustMidiEvent.SystemReset(renderer.SequencerTick));

                _uploadHooks[consoleUid] = hook;
            }

            var isBroadcaster = instrument.IsMidiOpen;
            var selection = console.Presets;

            if (hook.WasBroadcaster != isBroadcaster)
            {
                ReleasePuppet(hook);
                hook.WasBroadcaster = isBroadcaster;
            }

            if (isBroadcaster)
            {
                renderer!.VelocityOverride = null;

                if (hook.LastAppliedPresets.Count > 0 && PresetSlotsDrifted(renderer!, hook.Slaves, hook.LastAppliedPresets))
                    hook.LastAppliedPresets.Clear();

                if (SelectionChanged(hook.LastAppliedPresets, selection))
                {
                    var presets = ResolvePresets(selection);
                    RebuildLayers(renderer!, hook.Slaves, presets, hook.LastAppliedPresets, subscribeToMaster: true);
                }
            }
            else
            {
                renderer!.VelocityOverride = 0;

                if (refreshRange)
                    hook.InRange = LocalPlayerInRange(consoleUid, hook.Puppet != null);

                if (hook.Puppet != null && (!hook.InRange || hook.Puppet.Disposed))
                    ReleasePuppet(hook);

                if (hook.InRange && hook.Puppet == null)
                {
                    var puppet = RentRenderer();
                    if (puppet != null)
                    {
                        puppet.DisableProgramChangeEvent = false;
                        puppet.SendMidiEvent(RobustMidiEvent.SystemReset(puppet.SequencerTick));
                        SeedRenderer(puppet, renderer.RendererState);
                        puppet.TrackingEntity = consoleUid;
                        hook.Puppet = puppet;
                        hook.LastAppliedPresets.Clear();
                    }
                }

                if (hook.Puppet is { Disposed: false } puppetMaster)
                {
                    if (checkRange && hook.LastAppliedPresets.Count > 0 && PresetSlotsDrifted(puppetMaster, hook.Slaves, hook.LastAppliedPresets))
                        hook.LastAppliedPresets.Clear();

                    if (SelectionChanged(hook.LastAppliedPresets, selection))
                    {
                        var presets = ResolvePresets(selection);
                        RebuildLayers(puppetMaster, hook.Slaves, presets, hook.LastAppliedPresets, subscribeToMaster: false,
                            seedState: renderer.RendererState);
                    }
                }
            }

            UpdatePuppetFeed(hook);

            var shouldUpload = instrument.IsMidiOpen && console.Broadcasting;

            if (shouldUpload && !hook.HandlerAttached)
            {
                renderer!.OnMidiEvent += hook.Handler;
                hook.HandlerAttached = true;
            }
            else if (!shouldUpload && hook.HandlerAttached)
            {
                renderer!.OnMidiEvent -= hook.Handler;
                hook.HandlerAttached = false;
                hook.Pending.Clear();
            }

            if (shouldUpload && hook.Pending.Count > 0)
            {
                var netConsole = GetNetEntity(consoleUid);
                var pending = hook.Pending;
                for (var start = 0; start < pending.Count; start += RadioBroadcastConstants.MaxUploadBatchSize)
                {
                    var count = Math.Min(RadioBroadcastConstants.MaxUploadBatchSize, pending.Count - start);
                    var batch = new RobustMidiEvent[count];
                    pending.CopyTo(start, batch, 0, count);
                    RaiseNetworkEvent(new RadioMidiUploadEvent(netConsole, batch));
                }
                pending.Clear();
            }
        }
    }

    private void OnConsoleShutdown(Entity<RadioBroadcastConsoleComponent> ent, ref ComponentShutdown args)
    {
        if (_uploadHooks.TryGetValue(ent.Owner, out var hook))
            DetachUploadHook(ent.Owner, hook);
    }

    private void DetachUploadHook(EntityUid consoleUid, UploadHook hook)
    {
        if (hook.HandlerAttached)
        {
            hook.Renderer.OnMidiEvent -= hook.Handler;
            hook.HandlerAttached = false;
        }
        if (hook.PuppetFeedAttached)
        {
            hook.Renderer.OnMidiEvent -= hook.PuppetFeed;
            hook.PuppetFeedAttached = false;
        }
        hook.Pending.Clear();
        ReleasePuppet(hook);

        if (!hook.Renderer.Disposed)
        {
            hook.Renderer.FilteredChannels.SetAll(false);
            hook.Renderer.VelocityOverride = null;
        }

        _uploadHooks.Remove(consoleUid);
    }

    private void OnRadioMidiRelay(RadioMidiRelayEvent ev)
    {
        // Stop the sound when the song stops, or the last note drones on.
        if (IsStopSignal(ev.MidiEvents))
        {
            foreach (var rr in _receiverRenderers.Values)
            {
                if (rr.Console != ev.Console || rr.Channel != ev.Channel || rr.Renderer.Disposed)
                    continue;

                rr.Renderer.StopAllNotes();
                foreach (var slave in rr.Slaves)
                {
                    if (!slave.Disposed)
                        slave.StopAllNotes();
                }
                rr.Anchored = false;
            }
            return;
        }

        var firstTick = ev.MidiEvents[0].Tick;
        for (var i = 1; i < ev.MidiEvents.Length; i++)
        {
            if (ev.MidiEvents[i].Tick < firstTick)
                firstTick = ev.MidiEvents[i].Tick;
        }

        var query = EntityQueryEnumerator<RadioReceiverComponent>();
        while (query.MoveNext(out var receiverUid, out var receiver))
        {
            if (!receiver.PoweredOn || receiver.Channel != ev.Channel)
                continue;

            var rr = GetOrCreateReceiverRenderer(receiverUid, ev.Console, ev.Channel);
            if (rr == null)
                continue;

            var volume = receiver.Volume;

            var needsReanchor = rr.Anchored && firstTick < rr.EventTickAnchor;
            if (!rr.Anchored || needsReanchor)
            {
                if (needsReanchor)
                {
                    FlushAndReapplyPresets(rr);
                }

                rr.EventTickAnchor = firstTick;
                var sqrtLag = MathF.Sqrt((_netManager.ServerChannel?.Ping ?? 0) / 1000f);
                var lead = (uint) (rr.Renderer.SequencerTimeScale * (.2 + sqrtLag));
                rr.RendererTickAnchor = rr.Renderer.SequencerTick + lead;
                rr.Anchored = true;
            }

            var currentTick = rr.Renderer.SequencerTick;
            for (uint i = 0; i < ev.MidiEvents.Length; i++)
            {
                var mev = ev.MidiEvents[i];

                if (volume < 1f && mev.MidiCommand == RobustMidiCommand.NoteOn && mev.Velocity > 0)
                {
                    var scaledVelocity = (byte) Math.Clamp((int) MathF.Round(mev.Velocity * volume), 0, 127);
                    mev = new RobustMidiEvent(mev.Status, mev.Data1, scaledVelocity, mev.Tick);
                }

                var scheduleAt = rr.RendererTickAnchor + (mev.Tick - rr.EventTickAnchor) + i;

                if (scheduleAt < currentTick)
                {
                    rr.RendererTickAnchor += currentTick - scheduleAt;
                    scheduleAt = currentTick;
                }

                rr.Renderer.ScheduleMidiEvent(mev, scheduleAt, absolute: true);

                var slaveDelay = scheduleAt > currentTick ? scheduleAt - currentTick : 0u;
                foreach (var slave in rr.Slaves)
                {
                    if (!slave.Disposed)
                        slave.ScheduleMidiEvent(mev, slaveDelay, absolute: false);
                }
            }
        }
    }

    private static bool IsStopSignal(RobustMidiEvent[] events)
        => events.Length == 1
           && events[0].MidiCommand == RobustMidiCommand.SystemMessage
           && events[0].Control == 0x0
           && events[0].Status == 0xFF;

    private void UpdateReceiverRenderers(bool checkPresets)
    {
        _reapBuffer.Clear();
        foreach (var (receiverUid, rr) in _receiverRenderers)
        {
            if (!ShouldKeepReceiver(receiverUid, rr))
            {
                _reapBuffer.Add(receiverUid);
                continue;
            }

            if (!checkPresets)
                continue;

            if (!TryGetEntity(rr.Console, out var consoleUid)
                || !TryComp<RadioBroadcastConsoleComponent>(consoleUid, out var consoleComp))
                continue;

            if (PresetSlotsDrifted(rr.Renderer, rr.Slaves, rr.LastAppliedPresets))
                rr.LastAppliedPresets.Clear();

            if (SelectionChanged(rr.LastAppliedPresets, consoleComp.Presets))
            {
                var presets = ResolvePresets(consoleComp.Presets);
                RebuildLayers(rr.Renderer, rr.Slaves, presets, rr.LastAppliedPresets, subscribeToMaster: false,
                    seedState: ConsoleSeedState(consoleUid.Value));
            }
        }

        foreach (var receiverUid in _reapBuffer)
        {
            if (_receiverRenderers.TryGetValue(receiverUid, out var rr))
                DetachReceiver(receiverUid, rr);
        }
    }

    private void FlushAndReapplyPresets(ReceiverRenderer rr)
    {
        var seedState = TryGetEntity(rr.Console, out var consoleUid) ? ConsoleSeedState(consoleUid.Value) : null;

        for (var i = 0; i < rr.LastAppliedPresets.Count; i++)
        {
            if (!_protos.TryIndex<RadioPresetPrototype>(rr.LastAppliedPresets[i], out var preset))
                continue;

            if (SlotRenderer(rr, i) is not { } target)
                continue;

            target.ClearAllEvents();
            ApplyPresetToRenderer(target, preset, seedState);
        }
    }

    private bool ShouldKeepReceiver(EntityUid receiverUid, ReceiverRenderer rr)
    {
        if (!TryComp<RadioReceiverComponent>(receiverUid, out var receiver))
            return false;
        if (!receiver.PoweredOn || receiver.Channel != rr.Channel)
            return false;
        if (!TryGetEntity(rr.Console, out var consoleUid)
            || !TryComp<RadioBroadcastConsoleComponent>(consoleUid, out var console)
            || !console.Broadcasting)
            return false;
        return true;
    }

    private ReceiverRenderer? GetOrCreateReceiverRenderer(EntityUid receiverUid, NetEntity console, int channel)
    {
        if (_receiverRenderers.TryGetValue(receiverUid, out var existing))
        {
            if (existing.Console == console)
            {
                existing.Channel = channel;
                return existing;
            }
            DetachReceiver(receiverUid, existing);
        }

        var renderer = RentRenderer();
        if (renderer == null)
            return null;

        renderer.DisableProgramChangeEvent = false;
        renderer.SendMidiEvent(RobustMidiEvent.SystemReset(renderer.SequencerTick));

        var consoleResolved = TryGetEntity(console, out var consoleUid);
        var seedState = consoleResolved ? ConsoleSeedState(consoleUid!.Value) : null;
        if (seedState != null)
            SeedRenderer(renderer, seedState.Value);

        renderer.TrackingEntity = receiverUid;

        var rr = new ReceiverRenderer
        {
            Renderer = renderer,
            Console = console,
            Channel = channel,
        };

        var selection = consoleResolved
            && TryComp<RadioBroadcastConsoleComponent>(consoleUid, out var consoleComp)
                ? consoleComp.Presets
                : new List<ProtoId<RadioPresetPrototype>>();

        var presets = ResolvePresets(selection);
        RebuildLayers(renderer, rr.Slaves, presets, rr.LastAppliedPresets, subscribeToMaster: false, seedState);

        _receiverRenderers[receiverUid] = rr;
        return rr;
    }

    private void DetachReceiver(EntityUid receiverUid, ReceiverRenderer rr)
    {
        ReleaseSlaves(rr.Slaves, 0);
        rr.LastAppliedPresets.Clear();

        if (!rr.Renderer.Disposed)
        {
            rr.Renderer.Master = null;
            rr.Renderer.SystemReset();
            rr.Renderer.ClearAllEvents();
            for (var c = 0; c < rr.Renderer.FilteredChannels.Count; c++)
                rr.Renderer.FilteredChannels[c] = true;
            _freeRenderers.Add(rr.Renderer);
        }

        _receiverRenderers.Remove(receiverUid);
    }

    private void OnReceiverShutdown(Entity<RadioReceiverComponent> ent, ref ComponentShutdown args)
    {
        if (_receiverRenderers.TryGetValue(ent.Owner, out var rr))
            DetachReceiver(ent.Owner, rr);
    }
}

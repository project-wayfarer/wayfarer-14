using Content.Server._CS.Body.Systems;
using Content.Server.Chat.Managers;
using Content.Shared._CS.Body.Components;
using Content.Shared._CS.Weapons.Ranged.Components;
using Content.Shared._WF.Traits;
using Content.Shared.Chat;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server._WF.Traits;

public sealed class ClayBodySystem : EntitySystem
{
    [Dependency] private readonly SizeManipulationSystem _sizeManip = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<WFClayBodyComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<WFClayBodyComponent, InteractUsingEvent>(OnInteractUsing);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<WFClayBodyComponent>();
        while (query.MoveNext(out var uid, out var clay))
        {
            if (clay.NextRegenTime == null)
                continue;

            if (_timing.CurTime < clay.NextRegenTime.Value)
                continue;

            // Try to regen one size step.
            if (!TryComp<SizeAffectedComponent>(uid, out var sizeComp))
            {
                // Nothing to regen – stop timer.
                clay.NextRegenTime = null;
                continue;
            }

            // Stop regen if already at or above original scale.
            if (sizeComp.ScaleMultiplier >= clay.OriginalScale - 0.001f)
            {
                clay.NextRegenTime = null;
                continue;
            }

            _sizeManip.TryChangeSizeForced(uid, SizeManipulatorMode.Grow);

            // Notify only the player via private chat.
            if (TryComp<ActorComponent>(uid, out var actor))
            {
                _chatManager.ChatMessageToOne(
                    ChatChannel.Emotes,
                    Loc.GetString("clay-body-regen-message"),
                    Loc.GetString("clay-body-regen-message"),
                    EntityUid.Invalid,
                    false,
                    actor.PlayerSession.Channel);
            }

            // Check again after growing – if still below original scale, schedule next tick.
            if (TryComp<SizeAffectedComponent>(uid, out var updatedSize) &&
                updatedSize.ScaleMultiplier >= clay.OriginalScale - 0.001f)
            {
                clay.NextRegenTime = null;
            }
            else
            {
                clay.NextRegenTime = _timing.CurTime + clay.RegenInterval;
            }
        }
    }

    private void OnGetAlternativeVerbs(EntityUid uid, WFClayBodyComponent clay, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        var verb = new AlternativeVerb
        {
            Text = Loc.GetString("clay-body-verb-pluck"),
            Act = () => PluckClay(uid, clay, args.User),
            Priority = 1,
        };

        args.Verbs.Add(verb);
    }

    private void PluckClay(EntityUid uid, WFClayBodyComponent clay, EntityUid user)
    {
        // Capture original scale on first pluck.
        if (!clay.OriginalScaleCaptured)
        {
            var sizeComp = EnsureComp<SizeAffectedComponent>(uid);
            clay.OriginalScale = sizeComp.ScaleMultiplier;
            clay.OriginalScaleCaptured = true;
        }

        // Attempt to shrink the target.
        if (!_sizeManip.TryChangeSizeForced(uid, SizeManipulatorMode.Shrink, user))
        {
            _popup.PopupEntity(Loc.GetString("clay-body-pluck-fail"), uid, user, PopupType.SmallCaution);
            return;
        }

        // Spawn a clay chunk and try to put it in the plucker's hand.
        var userXform = Transform(user);
        var chunk = Spawn("WFClayChunk", userXform.Coordinates);
        if (!_hands.TryPickupAnyHand(user, chunk))
        {
            // No free hand — it stays on the ground where it spawned.
        }

        _popup.PopupEntity(Loc.GetString("clay-body-pluck-success-user"), uid, user, PopupType.Medium);
        if (uid != user)
            _popup.PopupEntity(Loc.GetString("clay-body-pluck-success-target"), uid, uid, PopupType.MediumCaution);

        if (TryComp<ActorComponent>(uid, out var pluckedActor))
        {
            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                Loc.GetString("clay-body-pluck-chat-target"),
                Loc.GetString("clay-body-pluck-chat-target"),
                EntityUid.Invalid,
                false,
                pluckedActor.PlayerSession.Channel);
        }

        // Start or refresh the regen timer.
        clay.NextRegenTime = _timing.CurTime + clay.RegenInterval;
    }

    private void OnInteractUsing(EntityUid uid, WFClayBodyComponent clay, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Only react to clay chunks.
        if (!HasComp<WFClayChunkComponent>(args.Used))
            return;

        args.Handled = true;

        // Grow the target — no cap at original scale; TryChangeSizeForced caps at MaxScale.
        if (!_sizeManip.TryChangeSizeForced(uid, SizeManipulatorMode.Grow, args.User))
        {
            _popup.PopupEntity(Loc.GetString("clay-body-add-fail"), uid, args.User, PopupType.SmallCaution);
            return;
        }

        // Consume the clay chunk.
        QueueDel(args.Used);

        _popup.PopupEntity(Loc.GetString("clay-body-add-success-user"), uid, args.User, PopupType.Medium);
        if (uid != args.User)
            _popup.PopupEntity(Loc.GetString("clay-body-add-success-target"), uid, uid, PopupType.Medium);

        if (TryComp<ActorComponent>(uid, out var addedActor))
        {
            _chatManager.ChatMessageToOne(
                ChatChannel.Emotes,
                Loc.GetString("clay-body-add-chat-target"),
                Loc.GetString("clay-body-add-chat-target"),
                EntityUid.Invalid,
                false,
                addedActor.PlayerSession.Channel);
        }

        // Cancel the regen timer if at or above original scale (no longer shrunk).
        if (TryComp<SizeAffectedComponent>(uid, out var updatedSize) &&
            clay.OriginalScaleCaptured &&
            updatedSize.ScaleMultiplier >= clay.OriginalScale - 0.001f)
        {
            clay.NextRegenTime = null;
        }
    }
}

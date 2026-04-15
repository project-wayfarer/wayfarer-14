using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.ActionBlocker;
using Content.Shared.Chat;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.InteractionVerbs;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.InteractionVerbs;

public sealed class InteractionVerbsSystem : SharedInteractionVerbsSystem
{
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlockerSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    protected override void TryPerformVerb(InteractionVerbPrototype proto, EntityUid user, EntityUid target)
    {
        if (!PrototypeManager.TryIndex(proto.ID, out InteractionVerbPrototype? verbProto))
            return;

        var hasHands = HasComp<HandsComponent>(user);
        var canAccess = _interactionSystem.InRangeUnobstructed(user, target);
        var canInteract = _actionBlockerSystem.CanInteract(user, target);

        // Get the active hand item if the user has hands
        EntityUid? usedItem = null;
        if (hasHands)
        {
            usedItem = _handsSystem.GetActiveItem(user);
        }

        var args = new InteractionArgs(user, target, usedItem, canAccess, canInteract, hasHands, null);

        // Check action
        if (verbProto.Action != null)
        {
            if (!verbProto.Action.IsAllowed(args, verbProto, _verbDependencies))
                return;

            if (!verbProto.Action.CanPerform(args, verbProto, true, _verbDependencies))
                return;
        }

        // Perform the action
        bool success = verbProto.Action?.Perform(args, verbProto, _verbDependencies) ?? true;

        // Show effects
        if (success && verbProto.EffectSuccess != null)
        {
            ShowEffects(verbProto, verbProto.EffectSuccess, InteractionPopupPrototype.Prefix.Success, args);
        }
        else if (!success && verbProto.EffectFailure != null)
        {
            ShowEffects(verbProto, verbProto.EffectFailure, InteractionPopupPrototype.Prefix.Fail, args);
        }

        // Do contact interaction
        if (success && verbProto.DoContactInteraction)
        {
            _interactionSystem.DoContactInteraction(user, target);
        }
    }

    private void ShowEffects(InteractionVerbPrototype proto, InteractionVerbPrototype.EffectSpecifier effect, InteractionPopupPrototype.Prefix prefix, InteractionArgs args)
    {
        if (effect.Popup != null && PrototypeManager.TryIndex(effect.Popup.Value, out var popupProto))
        {
            var hasUsed = args.Used != null;
            var selfMessage = Loc.GetString($"interaction-{proto.ID}-{prefix.ToString().ToLower()}-{popupProto.SelfSuffix}-popup",
                ("user", args.User),
                ("target", args.Target),
                ("used", args.Used ?? EntityUid.Invalid),
                ("selfTarget", args.User == args.Target),
                ("hasUsed", hasUsed));

            var targetMessage = popupProto.TargetSuffix != null
                ? Loc.GetString($"interaction-{proto.ID}-{prefix.ToString().ToLower()}-{popupProto.TargetSuffix}-popup",
                    ("user", args.User),
                    ("target", args.Target),
                    ("used", args.Used ?? EntityUid.Invalid),
                    ("selfTarget", args.User == args.Target),
                    ("hasUsed", hasUsed))
                : null;

            var othersMessage = popupProto.OthersSuffix != null
                ? Loc.GetString($"interaction-{proto.ID}-{prefix.ToString().ToLower()}-{popupProto.OthersSuffix}-popup",
                    ("user", args.User),
                    ("target", args.Target),
                    ("used", args.Used ?? EntityUid.Invalid),
                    ("selfTarget", args.User == args.Target),
                    ("hasUsed", hasUsed))
                : null;

            // Show popup to user
            _popupSystem.PopupEntity(selfMessage, args.Target, args.User, PopupType.Medium);

            // Show popup to target if different from user
            if (args.User != args.Target && targetMessage != null)
            {
                _popupSystem.PopupEntity(targetMessage, args.Target, args.Target, PopupType.Medium);
            }

            // Show popup to others
            if (othersMessage != null)
            {
                var filter = Filter.PvsExcept(args.User).RemoveWhere(s => s.AttachedEntity == args.Target);
                _popupSystem.PopupEntity(othersMessage, args.Target, filter, true, PopupType.Medium);
            }

            // Also send the message to chat as an emote using a dedicated key that does NOT include the
            // actor's name, since the chat system prepends it automatically via chat-manager-entity-me-wrap-message.
            if (popupProto.EmoteSuffix != null)
            {
                var chatMessage = Loc.GetString($"interaction-{proto.ID}-{prefix.ToString().ToLower()}-{popupProto.EmoteSuffix}-popup",
                    ("user", args.User),
                    ("target", args.Target),
                    ("used", args.Used ?? EntityUid.Invalid),
                    ("selfTarget", args.User == args.Target),
                    ("hasUsed", hasUsed));

                _chatSystem.TrySendInGameICMessage(args.User, chatMessage, InGameICChatType.Emote, ChatTransmitRange.Normal,
                    nameOverride: null, ignoreActionBlocker: true);
            }
        }

        // Play sound
        if (effect.Sound != null)
        {
            if (effect.SoundPerceivedByOthers)
            {
                _audioSystem.PlayPvs(effect.Sound, args.Target, effect.SoundParams);
            }
            else
            {
                _audioSystem.PlayEntity(effect.Sound, Filter.Entities(args.User, args.Target), args.Target, true, effect.SoundParams);
            }
        }
    }
}

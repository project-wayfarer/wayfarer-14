using Content.Shared.ActionBlocker;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Timing;

namespace Content.Shared.InteractionVerbs;

public abstract class SharedInteractionVerbsSystem : EntitySystem
{
    [Dependency] protected readonly IPrototypeManager PrototypeManager = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISerializationManager _serializationManager = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly INetManager _netManager = default!;

    protected InteractionAction.VerbDependencies _verbDependencies = default!;

    public override void Initialize()
    {
        base.Initialize();

        _verbDependencies = new InteractionAction.VerbDependencies(
            EntityManager,
            PrototypeManager,
            _random,
            _timing,
            _serializationManager
        );

        SubscribeLocalEvent<InteractionVerbsComponent, GetVerbsEvent<InteractionVerb>>(OnGetInteractionVerbs);
    }

    private void OnGetInteractionVerbs(EntityUid uid, InteractionVerbsComponent component, GetVerbsEvent<InteractionVerb> args)
    {
        // Don't show verbs if we can't interact
        if (!args.CanInteract && !args.CanAccess)
            return;

        var user = args.User;
        var target = uid;
        var hasHands = args.Hands != null;

        // Get all applicable interaction verb prototypes
        foreach (var verbProtoId in component.AllowedVerbs)
        {
            if (!PrototypeManager.TryIndex(verbProtoId, out var proto))
                continue;

            // Check if verb is allowed for this target
            if (!IsVerbApplicable(proto, user, target, hasHands, args.CanAccess, args.CanInteract))
                continue;

            var verb = CreateVerb(proto, user, target, hasHands, args.CanAccess, args.CanInteract);
            if (verb != null)
                args.Verbs.Add(verb);
        }

        // Handle global verbs
        foreach (var proto in PrototypeManager.EnumeratePrototypes<InteractionVerbPrototype>())
        {
            if (!proto.Global)
                continue;

            if (component.AllowedVerbs.Contains(proto.ID))
                continue; // Already added

            if (!IsVerbApplicable(proto, user, target, hasHands, args.CanAccess, args.CanInteract))
                continue;

            var verb = CreateVerb(proto, user, target, hasHands, args.CanAccess, args.CanInteract);
            if (verb != null)
                args.Verbs.Add(verb);
        }
    }

    private bool IsVerbApplicable(InteractionVerbPrototype proto, EntityUid user, EntityUid target, bool hasHands, bool canAccess, bool canInteract)
    {
        if (proto.Abstract)
            return false;

        if (!proto.AllowSelfInteract && user == target)
            return false;

        if (proto.RequiresHands && !hasHands)
            return false;

        if (proto.RequiresCanAccess && !canAccess)
            return false;

        // Check range
        var transform = Transform(user);
        var targetTransform = Transform(target);
        if (!transform.Coordinates.TryDistance(EntityManager, targetTransform.Coordinates, out var distance))
            return false;

        if (distance < proto.Range.Min || distance > proto.Range.Max)
            return false;

        return true;
    }

    private InteractionVerb? CreateVerb(InteractionVerbPrototype proto, EntityUid user, EntityUid target, bool hasHands, bool canAccess, bool canInteract)
    {
        var args = new InteractionArgs(user, target, null, canAccess, canInteract, hasHands, null);

        // Check requirement
        if (proto.Requirement != null && !proto.Requirement.IsMet(args, proto, _verbDependencies))
        {
            if (proto.HideByRequirement)
                return null;
        }

        // Server-only action check happens server-side
        var verb = new InteractionVerb
        {
            Text = proto.Name,
            Message = proto.Description,
            Icon = proto.Icon,
            Priority = proto.Priority,
            Act = () => TryPerformVerb(proto, user, target)
        };

        // Set category
        if (!string.IsNullOrEmpty(proto.CategoryKey))
        {
            verb.Category = GetVerbCategory(proto.CategoryKey);
        }
        else
        {
            verb.Category = VerbCategory.Interaction;
        }

        return verb;
    }

    private VerbCategory GetVerbCategory(string categoryKey)
    {
        return categoryKey switch
        {
            "interaction" => VerbCategory.Interaction,
            _ => VerbCategory.Interaction
        };
    }

    protected virtual void TryPerformVerb(InteractionVerbPrototype proto, EntityUid user, EntityUid target)
    {
        // This will be implemented in server-side system
    }
}

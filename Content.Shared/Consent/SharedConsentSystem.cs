using Content.Shared.Examine;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Verbs;
using Robust.Shared.Utility;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;


namespace Content.Shared.Consent;

public abstract partial class SharedConsentSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly ExamineSystemShared _examineSystem = default!;

    public override void Initialize()
    {
        // Commented out - replaced by Character examine button
        // SubscribeLocalEvent<MindContainerComponent, GetVerbsEvent<ExamineVerb>>(OnGetExamineVerbs);
    }

    /* Commented out - replaced by Character examine button
    private void OnGetExamineVerbs(Entity<MindContainerComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (_mindSystem.GetMind(ent, ent) is not { } mind
            || !TryComp<MindComponent>(mind, out var mindComponent)
            || mindComponent.UserId is not { } userId)
        {
            return;
        }

        // Check if there's any consent info to show
        var consentMessage = GetConsentText(userId);
        if (consentMessage.IsEmpty)
        {
            return; // Don't show the verb if there's no consent info
        }

        var user = args.User;

        args.Verbs.Add(new()
        {
            Text = Loc.GetString("consent-examine-verb"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/settings.svg.192dpi.png")),
            Act = () =>
            {
                var message = GetConsentText(userId);
                _examineSystem.SendExamineTooltip(user, ent, message, getVerbs: false, centerAtCursor: false);
            },
            Category = VerbCategory.Examine,
            CloseMenu = true,
        });
    }
    */

    protected virtual FormattedMessage GetConsentText(NetUserId userId)
    {
        return new FormattedMessage();
    }

    public virtual bool HasConsent(Entity<MindContainerComponent?> ent, ProtoId<ConsentTogglePrototype> consentId)
    {
        return false; // Implemented only on server side, prediction is *just a week away*
    }
}

using Content.Shared.Consent;
using Content.Server.Mind;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.Consent;

public sealed class ConsentSystem : SharedConsentSystem
{
    [Dependency] private readonly IServerConsentManager _consent = default!;
    [Dependency] private readonly MindSystem _serverMindSystem = default!;

    protected override FormattedMessage GetConsentText(NetUserId userId)
    {
        var consentSettings = _consent.GetPlayerConsentSettings(userId);
        var characterText = consentSettings.CharacterFreetext;
        var accountText = consentSettings.Freetext;

        // If both are empty, return empty message (verb won't be shown)
        if (string.IsNullOrWhiteSpace(characterText) && string.IsNullOrWhiteSpace(accountText))
        {
            return FormattedMessage.Empty;
        }

        var message = new FormattedMessage();

        // Show character-specific text first if it exists
        if (!string.IsNullOrWhiteSpace(characterText))
        {
            message.AddText(characterText);
        }

        // Show account text after if it exists
        if (!string.IsNullOrWhiteSpace(accountText))
        {
            if (!string.IsNullOrWhiteSpace(characterText))
            {
                message.AddText("\n\n");
            }
            message.AddText(accountText);
        }

        return message;
    }

    public override bool HasConsent(Entity<MindContainerComponent?> ent, ProtoId<ConsentTogglePrototype> consentId)
    {
        if (!Resolve(ent, ref ent.Comp)
            || _serverMindSystem.GetMind(ent, ent) is not { } mind)
        {
            return true; // NPCs as well as player characters without a mind consent to everything
        }

        if (!TryComp<MindComponent>(mind, out var mindComponent)
            || mindComponent.UserId is not { } userId)
        {
            // Not sure if this is ever reached? MindComponent seems to always have UserId.
            Log.Warning("HasConsent No UserId or missing MindComponent");
            return false; // For entities that have a mind but with no user attached, consent to nothing.
        }

        return _consent.GetPlayerConsentSettings(userId).Toggles.TryGetValue(consentId, out var val) && val == "on";
    }
}

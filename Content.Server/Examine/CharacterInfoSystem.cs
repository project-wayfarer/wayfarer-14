using Content.Server.Consent;
using Content.Server.Mind;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Consent;
using Content.Shared.DetailExaminable;
using Content.Shared.Examine;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.Player;

namespace Content.Server.Examine;

/// <summary>
/// Handles character information requests and sends character data to clients
/// </summary>
public sealed class CharacterInfoSystem : EntitySystem
{
    [Dependency] private readonly IServerConsentManager _consentManager = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly SharedIdCardSystem _idCardSystem = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RequestCharacterInfoEvent>(HandleCharacterInfoRequest);
    }

    private void HandleCharacterInfoRequest(RequestCharacterInfoEvent message, EntitySessionEventArgs args)
    {
        var entity = GetEntity(message.Entity);
        if (!Exists(entity))
            return;

        var response = new CharacterInfoEvent
        {
            Entity = message.Entity
        };

        // Get character name
        if (TryComp<MetaDataComponent>(entity, out var meta))
        {
            response.CharacterName = meta.EntityName;
        }

        // Check if player is connected and get mind info
        EntityUid? mindEntity = null;
        MindComponent? mindComp = null;

        if (TryComp<MindContainerComponent>(entity, out var mindContainer)
            && _mindSystem.GetMind(entity, mindContainer) is { } mind
            && TryComp<MindComponent>(mind, out var mindComponent))
        {
            mindEntity = mind;
            mindComp = mindComponent;
        }

        // If player is disconnected (SSD), show a message instead
        if (mindComp == null || mindComp.UserId == null || !_playerManager.TryGetSessionById(mindComp.UserId.Value, out _))
        {
            response.Description = Loc.GetString("character-window-ssd");
            response.ConsentText = Loc.GetString("character-window-ssd");
            RaiseNetworkEvent(response, args.SenderSession);
            return;
        }

        // Get job title from ID card
        if (_idCardSystem.TryFindIdCard(entity, out var idCard) && idCard.Comp.LocalizedJobTitle != null)
        {
            response.JobTitle = idCard.Comp.LocalizedJobTitle;
        }

        // Get description (flavor text)
        if (TryComp<DetailExaminableComponent>(entity, out var detailExaminable))
        {
            response.Description = detailExaminable.Content;
        }

        // Get consent text using the mind we already retrieved
        var consentSettings = _consentManager.GetPlayerConsentSettings(mindComp.UserId.Value);
        var characterText = consentSettings.CharacterFreetext;
        var accountText = consentSettings.Freetext;

        // Build consent text (character-specific first, then account)
        if (!string.IsNullOrWhiteSpace(characterText))
        {
            response.ConsentText = characterText;
        }

        if (!string.IsNullOrWhiteSpace(accountText))
        {
            if (!string.IsNullOrWhiteSpace(characterText))
            {
                response.ConsentText += "\n\n";
            }
            response.ConsentText += accountText;
        }

        RaiseNetworkEvent(response, args.SenderSession);
    }
}

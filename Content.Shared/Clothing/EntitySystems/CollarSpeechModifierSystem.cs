using Content.Shared.Clothing.Components;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Verbs;

namespace Content.Shared.Clothing.EntitySystems;

/// <summary>
/// System that overrides the wearer's speech with other text
/// </summary>
public sealed class CollarSpeechModifierSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CollarSpeechModifierComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CollarSpeechModifierComponent, GetVerbsEvent<ActivationVerb>>(AddTypeVerbs);
    }

    private void AddTypeVerbs(Entity<CollarSpeechModifierComponent> ent, ref GetVerbsEvent<ActivationVerb> args)
    {
        var category = new VerbCategory(Loc.GetString("collar-speech-select"), (string?)null);
        var user = args.User;
        foreach (var key in ent.Comp.SpeechTypes.Keys)
        {
            var selectVerb = new ActivationVerb
            {
                Category = category,
                Text = key,
                Act = () =>
                {
                    var comp = Comp<CollarSpeechModifierComponent>(ent);
                    comp.SelectedType = key;
                    _popupSystem.PopupClient(Loc.GetString("collar-speech-set", ("key", key)), user);
                },
                Impact = LogImpact.Low,
            };
            selectVerb.Impact = LogImpact.Low;
            args.Verbs.Add(selectVerb);
        }

    }

    private void OnExamined(Entity<CollarSpeechModifierComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("collar-speech-examine", ("mode", ent.Comp.SelectedType ?? Loc.GetString("collar-speech-unknown"))));
    }
}

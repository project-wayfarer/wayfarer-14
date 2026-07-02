using Content.Shared.FloofStation;
using Content.Shared.Implants;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs;
using Content.Shared.Popups;
using Content.Shared.Trigger.Components.Triggers;

using Content.Shared.Verbs; // Wayfarer


namespace Content.Shared.Trigger.Systems;

public sealed partial class TriggerOnMobstateChangeSystem : EntitySystem
{
    private void OnVerbRelay(EntityUid uid,
        TriggerOnMobstateChangeComponent component,
        ImplantRelayEvent<GetVerbsEvent<Verb>> args)
    {
        OnGetVerbs(uid, component, args.Event);
    }

    private void OnGetVerbs(EntityUid uid,
        TriggerOnMobstateChangeComponent component,
        GetVerbsEvent<Verb> args)
    {
        if (args.User != args.Target)
            return; // Self only, but usable in crit

        // I can't for the life of me stop this from displaying the popup twice. Is what it is.
        var verb = new Verb()
        {
            Text = Loc.GetString(
                "trigger-on-mobstate-verb-text",
                ("implant", Name(uid)),
                ("state", component.Enabled ? "ON" : "OFF")),
            Act = () =>
            {
                component.Enabled = !component.Enabled;
                _popup.PopupEntity(
                    Loc.GetString(
                        "trigger-on-mobstate-verb-popup",
                        ("state", component.Enabled ? "ENABLED" : "DISABLED")),
                    args.User,
                    args.User);
            },
            Disabled = false,
            Message = Loc.GetString("trigger-on-mobstate-verb-description"),
        };
        args.Verbs.Add(verb);
    }
}

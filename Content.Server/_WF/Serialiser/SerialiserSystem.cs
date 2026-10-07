using System.Text;
using Content.Server.Administration.Logs;
using Content.Server.Power.EntitySystems;
using Content.Server._WF.OutlawObjectives;
using Content.Shared._WF.Serialiser;
using Content.Shared.Access.Systems;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Content.Shared.Armor;

namespace Content.Server._WF.Serialiser;

public sealed class SerialiserSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private const string SerialChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SerialiserComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<SerialiserComponent, SerialiseDoAfterEvent>(OnDoAfter);
        SubscribeLocalEvent<SerialNumberComponent, ExaminedEvent>(OnExamined);
    }

    private void OnInteractUsing(Entity<SerialiserComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !CanSerialise(ent, args.Used))
            return;

        args.Handled = true;

        if (!_access.IsAllowed(args.User, ent))
        {
            _popup.PopupEntity(Loc.GetString("serialiser-access-denied"), ent, args.User);
            _audio.PlayPvs(ent.Comp.DenySound, ent);
            return;
        }

        if (!_doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new SerialiseDoAfterEvent(), ent, target: ent, used: args.Used)
            {
                BreakOnMove = true,
                BreakOnDropItem = true,
                BreakOnHandChange = true,
                NeedHand = true,
            }))
        {
            return;
        }

        _appearance.SetData(ent, SerialiserVisuals.Running, true);
        ent.Comp.PlayingSound = _audio.PlayPvs(ent.Comp.WorkingSound, ent)?.Entity;
    }

    private void OnDoAfter(Entity<SerialiserComponent> ent, ref SerialiseDoAfterEvent args)
    {
        _appearance.SetData(ent, SerialiserVisuals.Running, false);
        ent.Comp.PlayingSound = _audio.Stop(ent.Comp.PlayingSound);

        if (args.Handled || args.Cancelled || args.Used is not { } used)
            return;

        if (!CanSerialise(ent, used) || !_access.IsAllowed(args.User, ent))
            return;

        var serial = GenerateSerial(ent.Comp.Prefix);
        AddComp<SerialNumberComponent>(used).Serial = serial;
        args.Handled = true;

        _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(args.User):player} serialised {ToPrettyString(used):item} as {serial}");
    }

    private void OnExamined(Entity<SerialNumberComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("serial-number-examine", ("serial", ent.Comp.Serial)));
    }

    private bool CanSerialise(Entity<SerialiserComponent> ent, EntityUid item)
    {
        return this.IsPowered(ent, EntityManager)
            && !HasComp<SerialNumberComponent>(item)
            && !HasComp<OutlawObjectiveItemComponent>(item)
            && (_whitelist.IsWhitelistPass(ent.Comp.Whitelist, item) || HasRealArmor(item))
            && !_whitelist.IsWhitelistPass(ent.Comp.Blacklist, item);
    }

    private bool HasRealArmor(EntityUid item)
    {
        return TryComp<ArmorComponent>(item, out var armor)
            && (armor.Modifiers.Coefficients.Count > 0 || armor.Modifiers.FlatReduction.Count > 0);
    }

    private string GenerateSerial(string prefix)
    {
        string serial;
        do
        {
            var builder = new StringBuilder(prefix);
            builder.Append('-');
            AppendRandom(builder, 4);
            builder.Append('-');
            AppendRandom(builder, 4);
            serial = builder.ToString();
        } while (IsTaken(serial));

        return serial;
    }

    private void AppendRandom(StringBuilder builder, int count)
    {
        for (var i = 0; i < count; i++)
        {
            builder.Append(SerialChars[_random.Next(SerialChars.Length)]);
        }
    }

    private bool IsTaken(string serial)
    {
        var query = EntityQueryEnumerator<SerialNumberComponent>();
        while (query.MoveNext(out var comp))
        {
            if (comp.Serial == serial)
                return true;
        }

        return false;
    }
}

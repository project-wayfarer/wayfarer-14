using Content.Shared.Clothing.Components;
using Content.Shared.Damage;
using Content.Shared.Examine;
using Content.Shared.Inventory;
using Content.Shared.Silicons.Borgs;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Armor;

/// <summary>
///     This handles logic relating to <see cref="ArmorComponent" />
/// </summary>
public abstract class SharedArmorSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<CoefficientQueryEvent>>(OnCoefficientQuery);
        SubscribeLocalEvent<ArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<ArmorComponent, BorgModuleRelayedEvent<DamageModifyEvent>>(OnBorgDamageModify);
        SubscribeLocalEvent<ArmorComponent, GetVerbsEvent<ExamineVerb>>(OnArmorVerbExamine);
    }

    /// <summary>
    /// Get the total Damage reduction value of all equipment caught by the relay.
    /// </summary>
    /// <param name="ent">The item that's being relayed to</param>
    /// <param name="args">The event, contains the running count of armor percentage as a coefficient</param>
    private void OnCoefficientQuery(Entity<ArmorComponent> ent, ref InventoryRelayedEvent<CoefficientQueryEvent> args)
    {
        foreach (var armorCoefficient in ent.Comp.Modifiers.Coefficients)
        {
            args.Args.DamageModifiers.Coefficients[armorCoefficient.Key] = args.Args.DamageModifiers.Coefficients.TryGetValue(armorCoefficient.Key, out var coefficient) ? coefficient * armorCoefficient.Value : armorCoefficient.Value;
        }
    }

    private void OnDamageModify(EntityUid uid, ArmorComponent component, InventoryRelayedEvent<DamageModifyEvent> args)
    {
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
    }

    private void OnBorgDamageModify(EntityUid uid, ArmorComponent component,
        ref BorgModuleRelayedEvent<DamageModifyEvent> args)
    {
        args.Args.Damage = DamageSpecifier.ApplyModifierSet(args.Args.Damage, component.Modifiers);
    }

    private void OnArmorVerbExamine(EntityUid uid, ArmorComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || !component.ShowArmorOnExamine)
            return;

        // Try to find armor in the same slot(s) that the examiner is currently wearing for comparison.
        DamageModifierSet? equippedModifiers = null;
        if (TryComp<ClothingComponent>(uid, out var clothingComp))
        {
            equippedModifiers = GetEquippedArmorModifiers(args.User, uid, clothingComp.Slots);
        }

        var examineMarkup = GetArmorExamine(component.Modifiers, equippedModifiers);

        var ev = new ArmorExamineEvent(examineMarkup);
        RaiseLocalEvent(uid, ref ev);

        _examine.AddDetailedExamineVerb(args, component, examineMarkup,
            Loc.GetString("armor-examinable-verb-text"), "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("armor-examinable-verb-message"));
    }

    /// <summary>
    /// Finds the <see cref="DamageModifierSet"/> of an armor item currently worn by <paramref name="user"/>
    /// in any slot matching <paramref name="itemSlotFlags"/>, excluding the item being examined itself.
    /// Returns null if nothing comparable is equipped.
    /// </summary>
    private DamageModifierSet? GetEquippedArmorModifiers(EntityUid user, EntityUid examinedItem, SlotFlags itemSlotFlags)
    {
        if (itemSlotFlags == SlotFlags.NONE || !_inventory.TryGetSlots(user, out var slots))
            return null;

        foreach (var slot in slots)
        {
            if ((slot.SlotFlags & itemSlotFlags) == 0)
                continue;

            if (!_inventory.TryGetSlotEntity(user, slot.Name, out var equipped))
                continue;

            // Don't compare the item against itself if it's already being worn.
            if (equipped == examinedItem)
                return null;

            if (TryComp<ArmorComponent>(equipped, out var armorComp))
                return armorComp.Modifiers;
        }

        return null;
    }

    private FormattedMessage GetArmorExamine(DamageModifierSet armorModifiers, DamageModifierSet? equippedModifiers = null)
    {
        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString(equippedModifiers != null ? "armor-examine-compare" : "armor-examine"));

        // Track which damage types we've already displayed.
        var displayed = new HashSet<string>();

        foreach (var coefficientArmor in armorModifiers.Coefficients)
        {
            msg.PushNewline();
            displayed.Add(coefficientArmor.Key);

            var armorType = Loc.GetString("armor-damage-type-" + coefficientArmor.Key.ToLower());
            var newValue = MathF.Round((1f - coefficientArmor.Value) * 100, 1);

            if (equippedModifiers != null && equippedModifiers.Coefficients.TryGetValue(coefficientArmor.Key, out var equippedCoefficient))
            {
                var currentValue = MathF.Round((1f - equippedCoefficient) * 100, 1);
                var diff = MathF.Round(newValue - currentValue, 1);
                var deltaColor = diff > 0f ? "green" : (diff < 0f ? "red" : "gray");
                var deltaSign = diff > 0f ? "+" : "";
                msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value-compare",
                    ("type", armorType),
                    ("value", newValue),
                    ("delta", $"{deltaSign}{diff}"),
                    ("deltaColor", deltaColor)
                ));
            }
            else
            {
                msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value",
                    ("type", armorType),
                    ("value", newValue)
                ));
            }
        }

        // Show types the examined item doesn't cover but the equipped item does (protection goes to 0).
        if (equippedModifiers != null)
        {
            foreach (var equippedCoefficient in equippedModifiers.Coefficients)
            {
                if (displayed.Contains(equippedCoefficient.Key))
                    continue;

                msg.PushNewline();
                var armorType = Loc.GetString("armor-damage-type-" + equippedCoefficient.Key.ToLower());
                var currentValue = MathF.Round((1f - equippedCoefficient.Value) * 100, 1);
                msg.AddMarkupOrThrow(Loc.GetString("armor-coefficient-value-compare",
                    ("type", armorType),
                    ("value", 0f),
                    ("delta", $"-{currentValue}"),
                    ("deltaColor", "red")
                ));
            }
        }

        foreach (var flatArmor in armorModifiers.FlatReduction)
        {
            msg.PushNewline();
            displayed.Add(flatArmor.Key);

            var armorType = Loc.GetString("armor-damage-type-" + flatArmor.Key.ToLower());

            if (equippedModifiers != null && equippedModifiers.FlatReduction.TryGetValue(flatArmor.Key, out var equippedFlat))
            {
                var diff = MathF.Round(flatArmor.Value - equippedFlat, 1);
                var deltaColor = diff > 0f ? "green" : (diff < 0f ? "red" : "gray");
                var deltaSign = diff > 0f ? "+" : "";
                msg.AddMarkupOrThrow(Loc.GetString("armor-reduction-value-compare",
                    ("type", armorType),
                    ("value", flatArmor.Value),
                    ("delta", $"{deltaSign}{diff}"),
                    ("deltaColor", deltaColor)
                ));
            }
            else
            {
                msg.AddMarkupOrThrow(Loc.GetString("armor-reduction-value",
                    ("type", armorType),
                    ("value", flatArmor.Value)
                ));
            }
        }

        // Show flat reductions the equipped item has but the examined item doesn't.
        if (equippedModifiers != null)
        {
            foreach (var equippedFlat in equippedModifiers.FlatReduction)
            {
                if (displayed.Contains(equippedFlat.Key))
                    continue;

                msg.PushNewline();
                var armorType = Loc.GetString("armor-damage-type-" + equippedFlat.Key.ToLower());
                msg.AddMarkupOrThrow(Loc.GetString("armor-reduction-value-compare",
                    ("type", armorType),
                    ("value", 0f),
                    ("delta", $"-{MathF.Round(equippedFlat.Value, 1)}"),
                    ("deltaColor", "red")
                ));
            }
        }

        return msg;
    }
}

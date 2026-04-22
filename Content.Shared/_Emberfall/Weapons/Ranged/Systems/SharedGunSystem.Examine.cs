using System.Diagnostics.CodeAnalysis;
using Content.Shared._NF.Weapons.Rarity; // Frontier
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Utility;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    private void OnGunVerbExamine(Entity<GunComponent> ent, ref GetVerbsEvent<ExamineVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess)
            return;

        // Find another gun in the examiner's hands to use as a comparison baseline.
        GunComponent? compareGun = null;
        foreach (var held in _hands.EnumerateHeld(args.User))
        {
            if (held == ent.Owner)
                continue;

            if (TryComp<GunComponent>(held, out var heldGun))
            {
                compareGun = heldGun;
                break;
            }
        }

        var examineMarkup = GetGunExamine(ent, compareGun);

        var ev = new GunExamineEvent(examineMarkup);
        RaiseLocalEvent(ent, ref ev);

        Examine.AddDetailedExamineVerb(args, // Frontier: use SharedGunSystem's examine member
            ent.Comp,
            examineMarkup,
            Loc.GetString("gun-examinable-verb-text"),
            "/Textures/Interface/VerbIcons/dot.svg.192dpi.png",
            Loc.GetString("gun-examinable-verb-message"));
    }

    /// <summary>
    /// Returns the effective rounds-per-second of a gun, accounting for burst mode.
    /// </summary>
    private static float GetEffectiveFireRate(GunComponent gun)
    {
        if (gun.SelectedMode == SelectiveFire.Burst)
            return gun.ShotsPerBurstModified / (gun.BurstCooldown + (gun.ShotsPerBurstModified - 1) / gun.BurstFireRate);
        return gun.FireRateModified;
    }

    private FormattedMessage GetGunExamine(Entity<GunComponent> ent, GunComponent? compareGun = null)
    {
        TryComp(ent.Owner, out RareWeaponComponent? rareComp); // Frontier: rare weapons

        var msg = new FormattedMessage();
        msg.AddMarkupOrThrow(Loc.GetString(compareGun != null ? "gun-examine-compare" : "gun-examine"));

        // Frontier: use nf-prefixed loc strings, no rounding on values
        // Recoil (AngleIncrease) — lower is better
        msg.PushNewline();
        var recoilVal = ent.Comp.AngleIncreaseModified.Degrees;
        if (compareGun != null)
        {
            var delta = Math.Round(recoilVal - compareGun.AngleIncreaseModified.Degrees, 1);
            var deltaColor = delta < 0d ? "green" : (delta > 0d ? "red" : "gray");
            var sign = delta > 0d ? "+" : "";
            msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-recoil-compare",
                ("color", FireRateExamineColor),
                ("value", recoilVal),
                ("delta", $"{sign}{delta}"),
                ("deltaColor", deltaColor)
            ));
        }
        else
        {
            msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-recoil",
                ("color", FireRateExamineColor),
                ("value", recoilVal)
            ));
        }
        PushStatModifier(msg, rareComp?.AccuracyModifier);

        // Stability (AngleDecay) — higher is better
        msg.PushNewline();
        var stabilityVal = ent.Comp.AngleDecayModified.Degrees;
        if (compareGun != null)
        {
            var delta = Math.Round(stabilityVal - compareGun.AngleDecayModified.Degrees, 1);
            var deltaColor = delta > 0d ? "green" : (delta < 0d ? "red" : "gray");
            var sign = delta > 0d ? "+" : "";
            msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-stability-compare",
                ("color", FireRateExamineColor),
                ("value", stabilityVal),
                ("delta", $"{sign}{delta}"),
                ("deltaColor", deltaColor)
            ));
        }
        else
        {
            msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-stability",
                ("color", FireRateExamineColor),
                ("value", stabilityVal)
            ));
        }
        PushStatModifier(msg, rareComp != null ? 1 / rareComp?.AccuracyModifier : null);

        // Max Angle — lower is better
        msg.PushNewline();
        var maxAngleVal = ent.Comp.MaxAngleModified.Degrees;
        if (compareGun != null)
        {
            var delta = Math.Round(maxAngleVal - compareGun.MaxAngleModified.Degrees, 1);
            var deltaColor = delta < 0d ? "green" : (delta > 0d ? "red" : "gray");
            var sign = delta > 0d ? "+" : "";
            msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-max-angle-compare",
                ("color", FireRateExamineColor),
                ("value", maxAngleVal),
                ("delta", $"{sign}{delta}"),
                ("deltaColor", deltaColor)
            ));
        }
        else
        {
            msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-max-angle",
                ("color", FireRateExamineColor),
                ("value", maxAngleVal)
            ));
        }
        PushStatModifier(msg, rareComp?.AccuracyModifier);

        // Min Angle — lower is better
        msg.PushNewline();
        var minAngleVal = ent.Comp.MinAngleModified.Degrees;
        if (compareGun != null)
        {
            var delta = Math.Round(minAngleVal - compareGun.MinAngleModified.Degrees, 1);
            var deltaColor = delta < 0d ? "green" : (delta > 0d ? "red" : "gray");
            var sign = delta > 0d ? "+" : "";
            msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-min-angle-compare",
                ("color", FireRateExamineColor),
                ("value", minAngleVal),
                ("delta", $"{sign}{delta}"),
                ("deltaColor", deltaColor)
            ));
        }
        else
        {
            msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-min-angle",
                ("color", FireRateExamineColor),
                ("value", minAngleVal)
            ));
        }
        PushStatModifier(msg, rareComp?.AccuracyModifier);

        // Frontier: separate burst fire calculation
        // Fire Rate — higher is better
        msg.PushNewline();
        if (ent.Comp.SelectedMode != SelectiveFire.Burst)
        {
            var fireRateVal = ent.Comp.FireRateModified;
            if (compareGun != null)
            {
                var compareRate = (double)GetEffectiveFireRate(compareGun);
                var delta = Math.Round(fireRateVal - compareRate, 1);
                var deltaColor = delta > 0d ? "green" : (delta < 0d ? "red" : "gray");
                var sign = delta > 0d ? "+" : "";
                msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-fire-rate-compare",
                    ("color", FireRateExamineColor),
                    ("value", fireRateVal),
                    ("delta", $"{sign}{delta}"),
                    ("deltaColor", deltaColor)
                ));
            }
            else
            {
                msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-fire-rate",
                    ("color", FireRateExamineColor),
                    ("value", fireRateVal)
                ));
            }
            PushStatModifier(msg, rareComp?.FireRateModifier);
        }
        else
        {
            var fireRate = ent.Comp.ShotsPerBurstModified / (ent.Comp.BurstCooldown + (ent.Comp.ShotsPerBurstModified - 1) / ent.Comp.BurstFireRate);
            if (compareGun != null)
            {
                var compareRate = (double)GetEffectiveFireRate(compareGun);
                var delta = Math.Round(fireRate - compareRate, 1);
                var deltaColor = delta > 0d ? "green" : (delta < 0d ? "red" : "gray");
                var sign = delta > 0d ? "+" : "";
                msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-fire-rate-burst-compare",
                    ("color", FireRateExamineColor),
                    ("value", fireRate),
                    ("burstsize", ent.Comp.ShotsPerBurstModified),
                    ("burstrate", ent.Comp.BurstFireRate),
                    ("delta", $"{sign}{delta}"),
                    ("deltaColor", deltaColor)
                ));
            }
            else
            {
                msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-fire-rate-burst",
                    ("color", FireRateExamineColor),
                    ("value", fireRate),
                    ("burstsize", ent.Comp.ShotsPerBurstModified),
                    ("burstrate", ent.Comp.BurstFireRate)
                ));
            }
        }
        // End Frontier: separate burst fire calculation

        // Muzzle Velocity (ProjectileSpeed) — higher is better
        msg.PushNewline();
        var muzzleVal = ent.Comp.ProjectileSpeedModified;
        if (compareGun != null)
        {
            var delta = Math.Round(muzzleVal - compareGun.ProjectileSpeedModified, 1);
            var deltaColor = delta > 0d ? "green" : (delta < 0d ? "red" : "gray");
            var sign = delta > 0d ? "+" : "";
            msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-muzzle-velocity-compare",
                ("color", FireRateExamineColor),
                ("value", muzzleVal),
                ("delta", $"{sign}{delta}"),
                ("deltaColor", deltaColor)
            ));
        }
        else
        {
            msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-muzzle-velocity",
                ("color", FireRateExamineColor),
                ("value", muzzleVal)
            ));
        }
        PushStatModifier(msg, rareComp?.ProjectileSpeedModifier);
        // End Frontier: use nf-prefixed loc strings, no rounding on values

        return msg;
    }

    // Frontier: show stat modifications
    private void PushStatModifier(FormattedMessage msg, float? maybeModifier)
    {
        // Assumption: The modification will be different *enough* from the base value
        // that we don't need to worry about floating-point precision nonsense.
        if (maybeModifier is { } modifier && modifier != 1.0f)
        {
            msg.AddText(" ");
            msg.AddMarkupOrThrow(Loc.GetString("gun-examine-nf-stat-modifier",
                ("difference", modifier - 1),
                ("plus", modifier > 1 ? "+" : "")
            ));
        }
    }
    // End Frontier

    private bool TryGetGunCaliber(EntityUid uid, GunComponent component, [NotNullWhen(true)] out string? caliber)
    {
        caliber = null;

        // Frontier change: Added ExamineCaliber to guns to note the caliber type in ftl
        if (!string.IsNullOrEmpty(component.ExamineCaliber))
        {
            var caliberName = Loc.GetString(component.ExamineCaliber);

            caliber = caliberName;
            return true;
        }

        return false;
    }

    private void InitializeGunExamine()
    {
        SubscribeLocalEvent<GunComponent, GetVerbsEvent<ExamineVerb>>(OnGunVerbExamine);
    }
}

/// <summary>
/// Event raised on a gun entity to get additional examine text relating to its specifications.
/// </summary>
[ByRefEvent]
public readonly record struct GunExamineEvent(FormattedMessage Msg);

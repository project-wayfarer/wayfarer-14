using Content.Server._NF.Radar;
using Content.Shared._CS.BlipCartridge;
using Content.Shared._NF.Radar;
using Content.Shared.CartridgeLoader;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Robust.Shared.Prototypes;

namespace Content.Server._CS.BlipCartridge;

/// <summary>
/// This system handles the Blip Cartridge, which adds a radar blip for your PDA!
/// You can customize it too!
/// </summary>
public sealed class BlipCartridgeSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public static readonly VerbCategory BlipPresetCat =
        new("verb-categories-blip-preset", (string?)null);

    public static readonly VerbCategory BlipColorCat =
        new("verb-categories-blip-color", (string?)null);

    public static readonly VerbCategory BlipShapeCat =
        new("verb-categories-blip-shape", (string?)null);

    public static readonly VerbCategory BlipSizeCat =
        new("verb-categories-blip-size", (string?)null);

    public static readonly VerbCategory BlipToggleCat =
        new("verb-categories-blip-toggle", (string?)null);

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BlipCartridgeComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<BlipCartridgeComponent, CartridgeAddedEvent>(OnCartridgeAdded);
        SubscribeLocalEvent<BlipCartridgeComponent, CartridgeRemovedEvent>(OnCartridgeRemoved);
        SubscribeLocalEvent<BlipCartridgeComponent, GetVerbsEvent<Verb>>(GetVerbs);
        // SubscribeLocalEvent<BlipCartridgeComponent, RadarBlipEvent>(UpdateBlipData); // todo, make it flash in crit
    }

    private void OnComponentInit(Entity<BlipCartridgeComponent> ent, ref ComponentInit args)
    {
        // All initial data should already be in the prototype YAML
        // Don't modify components during initialization to satisfy test requirements
    }

    private void OnCartridgeAdded(Entity<BlipCartridgeComponent> ent, ref CartridgeAddedEvent args)
    {
        // Add RadarBlipComponent to the loader (PDA) when cartridge is inserted
        var blip = EnsureComp<RadarBlipComponent>(args.Loader);
        // Copy settings from cartridge to the PDA's blip
        if (TryComp<RadarBlipComponent>(ent.Owner, out var cartridgeBlip))
        {
            blip.RadarColor = cartridgeBlip.RadarColor;
            blip.HighlightedRadarColor = cartridgeBlip.HighlightedRadarColor;
            blip.Shape = cartridgeBlip.Shape;
            blip.Scale = cartridgeBlip.Scale;
            blip.Enabled = cartridgeBlip.Enabled;
            blip.RequireNoGrid = cartridgeBlip.RequireNoGrid;
            blip.VisibleFromOtherGrids = cartridgeBlip.VisibleFromOtherGrids;
        }
    }

    private void OnCartridgeRemoved(Entity<BlipCartridgeComponent> ent, ref CartridgeRemovedEvent args)
    {
        RemComp<RadarBlipComponent>(args.Loader);
    }


    /// <summary>
    /// Take the data from the BlipCartridgeComponent and apply it to the RadarBlipComponent.
    /// </summary>
    private void LoadStoredBlipData(Entity<BlipCartridgeComponent> comp, bool initial = false)
    {
        var blip = EnsureComp<RadarBlipComponent>(comp.Owner); // Ensure the RadarBlipComponent is present
        var cartridge = comp.Comp;
        if (initial)
        {
            ApplyPresetBlipData(
                blip,
                cartridge,
                cartridge.DefaultPreset);
            blip.Enabled = cartridge.Enabled;
        }
        else
        {
            LoadBlipColorData(blip, cartridge);
            LoadBlipShapeData(blip, cartridge);
            LoadBlipScaleData(blip, cartridge);
        }

        LoadDefaultBlipData(blip, cartridge);
    }

    private void ApplyPresetBlipData(
        RadarBlipComponent blip,
        BlipCartridgeComponent cartridge,
        ProtoId<RadarBlipPresetPrototype> presetProto)
    {
        var safety = 3; // Safety counter to prevent infinite loops
        while (safety-- > 0)
        {
            if (_prototype.TryIndex(presetProto, out var preset))
            {
                cartridge.BlipColor = preset.ColorSet;
                cartridge.BlipShape = preset.ShapeSet;
                cartridge.Scale = preset.Scale;
                LoadBlipColorData(blip, cartridge);
                LoadBlipShapeData(blip, cartridge);
                LoadBlipScaleData(blip, cartridge);
                cartridge.CurrentPreset = presetProto; // Update the current preset
            }
            else
            {
                Log.Warning(
                    $"BlipCartridge {cartridge} has an invalid RadarBlipPreset: "
                    + $"{presetProto}. Using default preset.");
                presetProto = "RadarBlipPresetDefault";
                continue;
            }

            return;
        }

        Log.Error($"Failed to load RadarBlipPreset after multiple attempts for cartridge {cartridge}.");
        blip.RadarColor = Color.Red; // Fallback color
        blip.HighlightedRadarColor = Color.OrangeRed; // Fallback highlighted color
        blip.Shape = RadarBlipShape.Circle; // Fallback shape
        blip.Scale = 1f; // Fallback scale
    }

    /// <summary>
    /// Takes the prototypes from the BlipCartridgeComponent and applies them to the RadarBlipComponent.
    /// </summary>
    /// <param name="blip"></param>
    /// <param name="cartridge"></param>
    private void LoadBlipColorData(RadarBlipComponent blip, BlipCartridgeComponent cartridge)
    {
        var safety = 3; // Safety counter to prevent infinite loops
        while (safety-- > 0)
        {
            if (_prototype.TryIndex(cartridge.BlipColor, out var colorSet))
            {
                blip.RadarColor = Color.FromName(colorSet.Color);
                blip.HighlightedRadarColor = Color.FromName(colorSet.HighlightedColor);
            }
            else
            {
                Log.Warning(
                    $"BlipCartridge {cartridge} has an invalid RadarBlipColorSet: "
                    + $"{cartridge.BlipColor}. Using default color.");
                cartridge.BlipColor = "BlipColorGreen"; // Default color set
                continue;
            }

            return; // Exit the loop if we successfully loaded the color set
        }

        Log.Error($"Failed to load BlipColorSet after multiple attempts for cartridge {cartridge}.");
        blip.RadarColor = Color.Red; // Fallback color
        blip.HighlightedRadarColor = Color.OrangeRed; // Fallback highlighted color
    }

    /// <summary>
    /// Takes the shape from the BlipCartridgeComponent and applies it to the RadarBlipComponent.
    /// </summary>
    /// <param name="blip"></param>
    /// <param name="cartridge"></param>
    private void LoadBlipShapeData(RadarBlipComponent blip, BlipCartridgeComponent cartridge)
    {
        if (_prototype.TryIndex(cartridge.BlipShape, out var shapeSet))
        {
            blip.Shape = Enum.Parse<RadarBlipShape>(shapeSet.Shape, true);
        }
        else
        {
            Log.Warning(
                $"BlipCartridge {cartridge} has an invalid RadarBlipShapeSet: "
                + $"{cartridge.BlipShape}. Using default shape.");
            blip.Shape = RadarBlipShape.Circle;
        }
    }

    /// <summary>
    /// Yeah it just sets the scale of the blip.
    /// </summary>
    /// <param name="blip"></param>
    /// <param name="cartridge"></param>
    /// <remarks>
    /// Bitch
    /// </remarks>
    private void LoadBlipScaleData(RadarBlipComponent blip, BlipCartridgeComponent cartridge)
    {
        blip.Scale = cartridge.Scale;
    }

    /// <summary>
    /// Just loads the default blip data, kinda pointless but whatever.
    /// </summary>
    private void LoadDefaultBlipData(RadarBlipComponent blip, BlipCartridgeComponent cartridge)
    {
        blip.RequireNoGrid = false; // Assuming this is always true for the blip
        blip.VisibleFromOtherGrids = true; // Assuming this is always true for the blip
    }

    /// <summary>
    /// Sets the blip to enabled or disabled.
    /// </summary>
    private void ToggleBlip(Entity<BlipCartridgeComponent> ent, RadarBlipComponent radBlip)
    {
        radBlip.Enabled = !radBlip.Enabled; // Toggle the enabled state
        LoadStoredBlipData(ent); // Reload the blip data to apply changes
    }

    /// <summary>
    /// Changes the blip preset to the given preset.
    /// </summary>
    private void ChangeBlipPreset(Entity<BlipCartridgeComponent> ent, ProtoId<RadarBlipPresetPrototype> presetProto)
    {
        var blipData = ent.Comp;
        ApplyPresetBlipData(
            EnsureComp<RadarBlipComponent>(ent.Owner), // Ensure the RadarBlipComponent is present
            blipData,
            presetProto); // Apply the preset data to the blip component)
    }

    /// <summary>
    /// Changes the blip color to the given color.
    /// </summary>
    private void ChangeBlipColor(Entity<BlipCartridgeComponent> ent, ProtoId<BlipColorSetPrototype> colorProto)
    {
        var blipData = ent.Comp;
        blipData.BlipColor = colorProto; // Update the blip color
        LoadStoredBlipData(ent); // Reload the blip data to apply changes
    }

    /// <summary>
    /// Changes the blip shape to the given shape.
    /// </summary>
    private void ChangeBlipShape(Entity<BlipCartridgeComponent> ent, ProtoId<BlipShapeSetPrototype> shapeProto)
    {
        var blipData = ent.Comp;
        blipData.BlipShape = shapeProto; // Update the blip shape
        LoadStoredBlipData(ent); // Reload the blip data to apply changes
    }

    /// <summary>
    /// Changes the blip scale to the given scale.
    /// </summary>
    /// <remarks>
    /// Eat my ass
    /// </remarks>
    private void ChangeBlipScale(Entity<BlipCartridgeComponent> ent, float scale)
    {
        var blipData = ent.Comp;
        blipData.Scale = scale; // Update the blip scale
        LoadStoredBlipData(ent); // Reload the blip data to apply changes
    }

    /// <summary>
    /// I had a dream last night that I was trying to make a UI work for this fukcing thing.
    /// Turns out, fcuk that, we don't need a UI, we have another godawful system thats easier
    /// to code
    /// </summary>
    private void GetVerbs(Entity<BlipCartridgeComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        // a few settings: Toggle the blip, change the preset, change the color, change the shape, change the scale
        // lets fucking do it
        var blipData = ent.Comp;
        var radBlip = EnsureComp<RadarBlipComponent>(ent.Owner);
        // the toggle blip verb
        var toggleBlipVerb = new Verb()
        {
            Text = radBlip.Enabled ? "ON" : "OFF",
            Category = BlipToggleCat,
            Act = () =>
            {
                ToggleBlip(ent, radBlip);
            },
        };
        args.Verbs.Add(toggleBlipVerb);
        // the change preset verb
        foreach (var preset in blipData.Presets)
        {
            _prototype.TryIndex(preset, out RadarBlipPresetPrototype? presetProto);
            if (presetProto == null)
                continue;
            var presetVerb = new Verb()
            {
                Text = $"{presetProto.Name}",
                Category = BlipPresetCat,
                Act = () =>
                {
                    ChangeBlipPreset(ent, preset);
                },
            };
            args.Verbs.Add(presetVerb);
        }

        // the change color verb
        foreach (var color in blipData.ColorTable)
        {
            _prototype.TryIndex(color, out BlipColorSetPrototype? colorProto);
            if (colorProto == null)
                continue;
            var colorVerb = new Verb()
            {
                Text = $"{colorProto.Name}",
                Category = BlipColorCat,
                // Priority = (15 - colorProto.Order), // Use the order defined in the prototype for sorting
                Act = () =>
                {
                    ChangeBlipColor(ent, color);
                },
            };
            args.Verbs.Add(colorVerb);
        }

        // the change shape verb
        foreach (var shape in blipData.ShapeTable)
        {
            _prototype.TryIndex(shape, out BlipShapeSetPrototype? shapeProto);
            if (shapeProto == null)
                continue;
            var shapeVerb = new Verb()
            {
                Text = $"{shapeProto.Name}",
                Category = BlipShapeCat,
                Act = () =>
                {
                    ChangeBlipShape(ent, shape);
                },
            };
            args.Verbs.Add(shapeVerb);
        }

        // the change scale verbs
        List<float> scales = new()
        {
            0.5f,
            1f,
            1.5f,
            2f,
            2.5f,
            3f,
            3.5f,
            4f,
        }; // call me a dry brain, i got stuff to do
        foreach (var scale in scales)
        {
            // first, the floats might look like 1.499999999999999, so we convert them into a string that shows 1.5
            var scaleString = scale.ToString("0.0");
            var scaleVerb = new Verb()
            {
                Text = $"x{scaleString}",
                Category = BlipSizeCat,
                Act = () =>
                {
                    ChangeBlipScale(ent, scale);
                },
            };
            args.Verbs.Add(scaleVerb);
        }
    }
}

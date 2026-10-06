using System.Diagnostics.CodeAnalysis;
using Content.Shared._WF.Shuttles.Events;

namespace Content.Client._WF.Shuttles;

public sealed class ShipValueSystem : EntitySystem
{
    public static readonly Color LabelColor = Color.Orange;

    public bool Enabled = true;

    private readonly Dictionary<NetEntity, string> _labels = new();
    private readonly Dictionary<NetEntity, string> _dcLabels = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<LootScannerValuesEvent>(OnValues);
    }

    public bool TryGetLabel(EntityUid grid, [NotNullWhen(true)] out string? label)
    {
        label = null;
        return Enabled && _labels.TryGetValue(GetNetEntity(grid), out label);
    }

    public bool TryGetDcLabel(EntityUid grid, [NotNullWhen(true)] out string? label)
    {
        label = null;
        return Enabled && _dcLabels.TryGetValue(GetNetEntity(grid), out label);
    }

    private void OnValues(LootScannerValuesEvent ev)
    {
        _labels.Clear();

        foreach (var (grid, gain) in ev.Values)
        {
            var (unit, suffix) = GetUnit(gain);
            var value = $"{(double) gain / unit:0.#}{suffix}";

            _labels[grid] = Loc.GetString("loot-scanner-value", ("value", value));
        }

        _dcLabels.Clear();

        foreach (var (grid, dc) in ev.DcValues)
            _dcLabels[grid] = Loc.GetString("loot-scanner-dc-value", ("value", dc));
    }

    private static (long Unit, string Suffix) GetUnit(long gain)
    {
        return gain >= 1_000_000_000 ? (1_000_000_000, "B")
            : gain >= 1_000_000 ? (1_000_000, "M")
            : (1000, "K");
    }
}

using Content.Shared._WF.Weather;
using Robust.Shared.GameStates;

namespace Content.Client._WF.Weather;

public sealed class WFWeatherExposureSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WFExposureComponent, ComponentHandleState>(OnHandleState);
    }

    private void OnHandleState(Entity<WFExposureComponent> ent, ref ComponentHandleState args)
    {
        var comp = ent.Comp;

        switch (args.Current)
        {
            case WFExposureDeltaState delta:
                Apply(comp, delta.Open, delta.Covered);
                break;

            case WFExposureState full:
                comp.Chunks.Clear();
                Apply(comp, full.Open, full.Covered);
                break;
        }
    }

    // The reverse of what the server sends, so the two have to change together.
    private static void Apply(WFExposureComponent comp, Dictionary<Vector2i, ulong> open,
        Dictionary<Vector2i, ulong> covered)
    {
        foreach (var (chunk, openToOutside) in open)
        {
            if (openToOutside == 0)
            {
                comp.Chunks.Remove(chunk);
                continue;
            }

            comp.Chunks[chunk] = new WFExposureChunk
            {
                OpenToOutside = openToOutside,
                OpenOverhead = openToOutside & ~covered.GetValueOrDefault(chunk),
            };
        }
    }
}

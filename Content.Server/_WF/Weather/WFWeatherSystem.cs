using Content.Shared._WF.Weather;
using Robust.Shared.GameStates;

namespace Content.Server._WF.Weather;

public sealed class WFWeatherSystem : SharedWFWeatherSystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WFWeatherComponent, ComponentGetState>(OnGetState);
    }

    private void OnGetState(Entity<WFWeatherComponent> ent, ref ComponentGetState args)
    {
        args.State = new WFWeatherComponentState(ent.Comp.Weather);
    }

    protected override void EndWeather(EntityUid uid, WFWeatherComponent comp, string protoId)
    {
        base.EndWeather(uid, comp, protoId);

        if (comp.Weather.Count == 0)
            RemCompDeferred<WFWeatherComponent>(uid);
    }
}

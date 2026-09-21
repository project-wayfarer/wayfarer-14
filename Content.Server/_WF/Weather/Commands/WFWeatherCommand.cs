using System.Linq;
using Content.Server.Administration;
using Content.Shared.Administration;
using Content.Shared.Weather;
using Robust.Shared.Console;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._WF.Weather.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class WFWeatherCommand : LocalizedEntityCommands
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protoMan = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly WFWeatherSystem _weather = default!;

    public override string Command => "wfweather";
    public override string Description => "Sets the weather on a map. Pass null to clear it.";
    public override string Help => $"Usage: {Command} <mapId> <weather or null> [seconds]";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length < 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (!int.TryParse(args[0], out var mapInt) || !_mapSystem.MapExists(new MapId(mapInt)))
        {
            shell.WriteError($"'{args[0]}' is not the id of a map that exists.");
            return;
        }

        WeatherPrototype? weather = null;
        if (!args[1].Equals("null") && !_protoMan.TryIndex(args[1], out weather))
        {
            shell.WriteError($"There is no weather called '{args[1]}'.");
            return;
        }

        TimeSpan? endTime = null;
        if (args.Length > 2)
        {
            if (!int.TryParse(args[2], out var seconds) || seconds <= 0)
            {
                shell.WriteError($"'{args[2]}' is not a number of seconds above zero.");
                return;
            }

            endTime = _timing.CurTime + TimeSpan.FromSeconds(seconds);
        }

        _weather.SetWeather(new MapId(mapInt), weather, endTime);
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        switch (args.Length)
        {
            case 1:
                return CompletionResult.FromHintOptions(CompletionHelper.MapIds(EntityManager), "<mapId>");
            case 2:
                var weathers = CompletionHelper.PrototypeIDs<WeatherPrototype>(true, _protoMan)
                    .Append(new CompletionOption("null", "Clears the weather"));
                return CompletionResult.FromHintOptions(weathers, "<weather>");
            case 3:
                return CompletionResult.FromHint("[seconds]");
            default:
                return CompletionResult.Empty;
        }
    }
}

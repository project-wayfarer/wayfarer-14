using System.Linq;
using Content.Server._WF.Weather;
using Content.Server.Administration;
using Content.Shared._WF.Weather;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Prototypes;

namespace Content.Server._WF.Administration.Commands;

[AdminCommand(AdminFlags.Fun)]
public sealed class WeatherSchedulerCommand : LocalizedCommands
{
    [Dependency] private readonly IEntityManager _entities = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override string Command => "wfweatherscheduler";
    public override string Description => "Turns a weather schedule on or off. Called with no arguments it lists every schedule.";
    public override string Help => $"Usage: {Command} <schedule> <on|off>";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var scheduler = _entities.System<WFWeatherSchedulerSystem>();

        if (args.Length == 0)
        {
            foreach (var schedule in _proto.EnumeratePrototypes<WFWeatherSchedulePrototype>().OrderBy(s => s.ID))
            {
                shell.WriteLine($"{schedule.ID}: {(scheduler.IsEnabled(schedule) ? "on" : "off")}");
            }

            return;
        }

        if (args.Length != 2)
        {
            shell.WriteError(Help);
            return;
        }

        if (!_proto.TryIndex<WFWeatherSchedulePrototype>(args[0], out var target))
        {
            shell.WriteError($"No weather schedule called {args[0]}.");
            return;
        }

        var mode = args[1].ToLowerInvariant();
        if (mode is not ("on" or "off"))
        {
            shell.WriteError(Help);
            return;
        }

        var enabled = mode == "on";
        scheduler.SetEnabled(target.ID, enabled);
        shell.WriteLine($"{target.ID} is now {(enabled ? "on" : "off")}.");
    }

    public override CompletionResult GetCompletion(IConsoleShell shell, string[] args)
    {
        return args.Length switch
        {
            1 => CompletionResult.FromHintOptions(
                CompletionHelper.PrototypeIdsLimited<WFWeatherSchedulePrototype>(args[0], _proto),
                "<schedule>"),
            2 => CompletionResult.FromHintOptions(["on", "off"], "<on|off>"),
            _ => CompletionResult.Empty,
        };
    }
}

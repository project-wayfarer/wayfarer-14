using Content.Server.Administration;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._WF.Corporations.Commands;

[AdminCommand(AdminFlags.Host)]
public sealed class SavePlayerStationsCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entManager = default!;

    public string Command => "savePlayerStations";
    public string Description => "Force-saves all active corporation player stations to disk immediately.";
    public string Help => "savePlayerStations";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var system = _entManager.System<CorporationStationSystem>();
        system.SaveAllStations();
        shell.WriteLine("Corporation player stations saved.");
    }
}

using Content.Server._WF.Corporations.AdminEui;
using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._WF.Corporations.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class CorpAdminCommand : LocalizedCommands
{
    [Dependency] private readonly EuiManager _euiManager = default!;

    public override string Command => "corpadmin";
    public override string Description => "Opens the corporation admin management panel.";
    public override string Help => "corpadmin";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError("This command cannot be run from the server console.");
            return;
        }

        var eui = new CorpAdminEui();
        _euiManager.OpenEui(eui, player);
    }
}

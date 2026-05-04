using Content.Server.Administration.UI;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Administration.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class NanoChatAdminCommand : IConsoleCommand
{
    public string Command => "nanochatadmin";

    public string Description => "Opens the NanoChat admin viewer to see all player messages";

    public string Help => $"{Command}";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        var player = shell.Player;
        if (player == null)
        {
            shell.WriteLine("This does not work from the server console.");
            return;
        }

        var eui = IoCManager.Resolve<EuiManager>();
        var ui = new NanoChatAdminEui();
        eui.OpenEui(ui, player);
    }
}

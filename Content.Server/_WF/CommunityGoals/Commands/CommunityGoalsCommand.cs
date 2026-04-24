using Content.Server.Administration;
using Content.Server.EUI;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server._WF.CommunityGoals.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class CommunityGoalsCommand : LocalizedCommands
{
    [Dependency] private readonly EuiManager _eui = default!;

    public override string Command => "communitygoals";
    public override string Description => "Opens the community goals admin panel.";
    public override string Help => $"Usage: {Command}";

    public override void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        var eui = new CommunityGoalsEui();
        _eui.OpenEui(eui, player);
    }
}

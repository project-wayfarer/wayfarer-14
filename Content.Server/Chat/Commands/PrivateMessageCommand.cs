using System.Linq;
using System.Linq;
using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Chat.Commands;

[AnyCommand]
internal sealed class PrivateMessageCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "private";
    public string Description => "Send a private message to another player.";
    public string Help => "private <username or character name> <message>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (args.Length < 2)
        {
            shell.WriteError("Usage: private <username or character name> <message>");
            return;
        }

        var targetIdentifier = args[0];
        var message = string.Join(" ", args.Skip(1)).Trim();

        if (string.IsNullOrEmpty(message))
        {
            shell.WriteError("Message cannot be empty!");
            return;
        }

        var pmSystem = _entityManager.System<PrivateMessageSystem>();
        pmSystem.SendPrivateMessage(player, targetIdentifier, message);
    }
}

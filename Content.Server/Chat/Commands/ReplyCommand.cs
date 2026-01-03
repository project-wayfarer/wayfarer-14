using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;

namespace Content.Server.Chat.Commands;

[AnyCommand]
internal sealed class ReplyCommand : IConsoleCommand
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public string Command => "reply";
    public string Description => "Reply to the last private message you received.";
    public string Help => "reply <message>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (shell.Player is not { } player)
        {
            shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
            return;
        }

        if (args.Length < 1)
        {
            shell.WriteError("Usage: reply <message>");
            return;
        }

        var message = string.Join(" ", args).Trim();

        if (string.IsNullOrEmpty(message))
        {
            shell.WriteError("Message cannot be empty!");
            return;
        }

        var pmSystem = _entityManager.System<PrivateMessageSystem>();
        pmSystem.SendReply(player, message);
    }
}

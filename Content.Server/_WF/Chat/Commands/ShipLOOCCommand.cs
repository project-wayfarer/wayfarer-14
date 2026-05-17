using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Server.Chat.Commands
{
    [AnyCommand]
    internal sealed class ShipOOCCommand : IConsoleCommand
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;

        public string Command => "shipooc";
        public string Description => "Send Ship Out Of Character chat messages.";
        public string Help => "shipooc <text>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (shell.Player is not { } player)
            {
                shell.WriteError(Loc.GetString("shell-cannot-run-command-from-server"));
                return;
            }

            if (player.AttachedEntity is not { Valid: true } entity)
                return;

            if (player.Status != SessionStatus.InGame)
                return;

            if (args.Length < 1)
                return;

            var message = string.Join(" ", args).Trim();
            if (string.IsNullOrEmpty(message))
                return;

            _entityManager.System<ChatSystem>()
            .TrySendInGameOOCMessage(
                entity,
                message,
                InGameOOCChatType.ShipOoc,
                false,
                shell,
                player);
        }
    }
}

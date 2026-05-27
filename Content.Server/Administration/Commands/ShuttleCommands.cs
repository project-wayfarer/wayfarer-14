using Content.Server.RoundEnd;
using Content.Shared.Administration;
using Content.Shared.Localizations;
using Robust.Server.GameObjects;
using Robust.Shared.Console;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Server.Administration.Commands
{
    [AdminCommand(AdminFlags.Round)]
    public sealed class CallShuttleCommand : LocalizedEntityCommands
    {
        [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;

        public override string Command => "callshuttle";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (args.Length == 1 && TimeSpan.TryParseExact(args[0], ContentLocalizationManager.TimeSpanMinutesFormats, LocalizationManager.DefaultCulture, out var timeSpan))
                _roundEndSystem.RequestRoundEnd(timeSpan, shell.Player?.AttachedEntity, false);

            else if (args.Length == 1)
                shell.WriteLine(Loc.GetString("shell-timespan-minutes-must-be-correct"));

            else
                _roundEndSystem.RequestRoundEnd(shell.Player?.AttachedEntity, false);
        }
    }

    [AdminCommand(AdminFlags.Round)]
    public sealed class RecallShuttleCommand : LocalizedEntityCommands
    {
        [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;

        public override string Command => "recallshuttle";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            _roundEndSystem.CancelRoundEndCountdown(shell.Player?.AttachedEntity, false);
        }
    }

    [AdminCommand(AdminFlags.Admin)]
    public sealed class SpawnBaronessCommand : LocalizedEntityCommands
    {
        [Dependency] private readonly IEntityManager _entities = default!;
        [Dependency] private readonly IEntitySystemManager _systems = default!;

        public override string Command => "spawnbaroness";

        public override void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            var player = shell.Player;
            if (player?.AttachedEntity == null)
            {
                shell.WriteError("You must be attached to an entity (aghost) to use this command.");
                return;
            }

            var transform = _entities.GetComponent<TransformComponent>(player.AttachedEntity.Value);
            var mapId = transform.MapID;

            if (mapId == MapId.Nullspace)
            {
                shell.WriteError("Cannot spawn shuttle in nullspace.");
                return;
            }

            var mapLoader = _systems.GetEntitySystem<MapLoaderSystem>();
            var transformSys = _systems.GetEntitySystem<TransformSystem>();
            var shuttleSys = _systems.GetEntitySystem<Content.Shared.Shuttles.Systems.SharedShuttleSystem>();
            var mapCoords = transformSys.GetMapCoordinates(transform);
            
            // Offset the spawn position slightly below the aghost
            var offset = new Vector2(mapCoords.Position.X, mapCoords.Position.Y - 10f);

            var path = new ResPath("/Maps/_NF/Shuttles/baroness.yml");
            
            if (mapLoader.TryLoadGrid(mapId, path, out var gridUid, offset: offset))
            {
                // Make it show up on IFF by marking it as a player shuttle
                if (_entities.TryGetComponent<Content.Server.Shuttles.Components.ShuttleComponent>(gridUid.Value, out var shuttle))
                {
                    shuttle.PlayerShuttle = true;
                }

                // Ensure it has an IFF component and add the IsPlayerShuttle flag
                shuttleSys.AddIFFFlag(gridUid.Value, Content.Shared.Shuttles.Components.IFFFlags.IsPlayerShuttle);
                
                shell.WriteLine($"Successfully spawned Baroness shuttle at {offset}. Grid UID: {gridUid}");
            }
            else
            {
                shell.WriteError("Failed to load Baroness shuttle.");
            }
        }
    }
}

using System.Numerics;
using Content.Server._Coyote.ShuttleCrewStatus; // Wayfarer
using Content.Server.Chat.Systems;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Random.Helpers;
using Content.Server.Shuttles.Components; // Wayfarer
using Robust.Server.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.StationEvents.Events;

public sealed class MeteorSwarmSystem : GameRuleSystem<MeteorSwarmComponent>
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly StationSystem _station = default!;

    protected override void Added(EntityUid uid, MeteorSwarmComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        base.Added(uid, component, gameRule, args);

        component.WaveCounter = component.Waves.Next(RobustRandom);
    }

    // Wayfarer: Plays the meteor event's announcement and sound, unless it was silenced.
    protected override void Started(EntityUid uid, MeteorSwarmComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (component.Silent)
            return;

        // we don't want to send to players who aren't in game (i.e. in the lobby)
        var allPlayersInGame = Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);

        if (component.Announcement is { } locId)
            _chat.DispatchFilteredAnnouncement(allPlayersInGame, Loc.GetString(locId), playSound: false, colorOverride: Color.Gold);

        _audio.PlayGlobal(component.AnnouncementSound, allPlayersInGame, true);
    }

    protected override void ActiveTick(EntityUid uid, MeteorSwarmComponent component, GameRuleComponent gameRule, float frameTime)
    {
        if (Timing.CurTime < component.NextWaveTime)
            return;

        component.NextWaveTime += TimeSpan.FromSeconds(component.WaveCooldown.Next(RobustRandom));

        // Wayfarer: Nothing to hit this wave, so skip it.
        if (!TryPickTargetGrid(component, out var grid))
            return;

        var mapId = Transform(grid).MapID;
        var playableArea = _physics.GetWorldAABB(grid);

        var minimumDistance = (playableArea.TopRight - playableArea.Center).Length() + 50f;
        var maximumDistance = minimumDistance + 100f;

        var center = playableArea.Center;

        var meteorsToSpawn = component.MeteorsPerWave.Next(RobustRandom);
        for (var i = 0; i < meteorsToSpawn; i++)
        {
            var spawnProto = RobustRandom.Pick(component.Meteors);

            var angle = component.NonDirectional
                ? RobustRandom.NextAngle()
                : new Random(uid.Id).NextAngle();

            var offset = angle.RotateVec(new Vector2((maximumDistance - minimumDistance) * RobustRandom.NextFloat() + minimumDistance, 0));

            // the line at which spawns occur is perpendicular to the offset.
            // This means the meteors are less likely to bunch up and hit the same thing.
            var subOffsetAngle = RobustRandom.Prob(0.5f)
                ? angle + Math.PI / 2
                : angle - Math.PI / 2;
            var subOffset = subOffsetAngle.RotateVec(new Vector2( (playableArea.TopRight - playableArea.Center).Length() / 3 * RobustRandom.NextFloat(), 0));

            var spawnPosition = new MapCoordinates(center + offset + subOffset, mapId);
            var meteor = Spawn(spawnProto, spawnPosition);
            var physics = Comp<PhysicsComponent>(meteor);
            _physics.ApplyLinearImpulse(meteor, -offset.Normalized() * component.MeteorVelocity * physics.Mass, body: physics);
        }

        component.WaveCounter--;
        if (component.WaveCounter <= 0)
        {
            ForceEndSelf(uid, gameRule);
        }
    }

    // Wayfarer: Sets which grid the meteors hit when targeted manually.
    public void SetTargetGrid(EntityUid ruleEntity, EntityUid? targetGrid, MeteorSwarmComponent? component = null)
    {
        if (!Resolve(ruleEntity, ref component))
            return;
        component.TargetGrid = targetGrid;
    }

    // Wayfarer: Prevents the server announcement when a meteor event is called manually.
    public void SetSilent(EntityUid ruleEntity, MeteorSwarmComponent? component = null)
    {
        if (!Resolve(ruleEntity, ref component))
            return;
        component.Silent = true;
    }

    // Wayfarer: Every grid a meteor swarm can hit. That is every POI and station, plus active player ships.
    public IEnumerable<EntityUid> GetTargetableGrids()
    {
        var seen = new HashSet<EntityUid>();

        foreach (var station in _station.GetStations())
        {
            if (_station.GetLargestGrid(station) is not { } grid)
                continue;
            if (TryComp<ShuttleCrewStatusComponent>(grid, out var crew) && !crew.HasActiveCrew)
                continue;
            if (seen.Add(grid))
                yield return grid;
        }

        // Catch any active player ship the station loop above missed.
        var ships = EntityQueryEnumerator<ShuttleCrewStatusComponent, ShuttleComponent>();
        while (ships.MoveNext(out var ship, out var status, out _))
            if (status.HasActiveCrew && seen.Add(ship))
                yield return ship;
    }

    // Wayfarer: Picks the grid the meteors hit. The target if one was set, otherwise a random valid target.
    private bool TryPickTargetGrid(MeteorSwarmComponent component, out EntityUid grid)
    {
        grid = default;

        if (component.TargetGrid is { } pinned && Exists(pinned))
        {
            grid = pinned;
            return true;
        }

        var pool = new List<EntityUid>(GetTargetableGrids());
        if (pool.Count == 0)
            return false;

        grid = RobustRandom.Pick(pool);
        return true;
    }
}

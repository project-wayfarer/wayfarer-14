using System.Numerics;
using Content.Shared._NF.CCVar;
using Robust.Shared.Map;

namespace Content.Server._NF.GameRule;
public sealed partial class PointOfInterestSystem
{
    // Spawn mooring points at four distinct distance bands.
    public void GenerateMooringPointsSemiRandom(MapId mapUid, List<PointOfInterestPrototype> mooringPrototypes, out List<EntityUid> mooringStations)
    {
        mooringStations = new List<EntityUid>();
        if (mooringPrototypes.Count == 0)
            return;

        var proto = mooringPrototypes[0];

        const int count = 4;
        float minDist = proto.MinimumDistance;
        float maxDist = proto.MaximumDistance;
        float jitter = 500f; // random variation for the distance
        int maxRetries = int.Max(_cfg.GetCVar(NFCCVars.POIPlacementRetries), 0);
        // Shamelessly stolen most of this code from other generators
        for (int i = 0; i < count; i++)
        {
            // Target distance equally spaced within the distances in the prototype yml, centered in each band + jittery for a touch of rando.
            float baseDist = minDist + (maxDist - minDist) * ((i + 0.5f) / count);
            float distance = Math.Clamp(baseDist + _random.NextFloat(-jitter, jitter), minDist, maxDist);

            Vector2 offset = default;
            bool validPosition = false;

            for (int retry = 0; retry <= maxRetries; retry++)
            {
                Angle angle = _random.NextAngle();
                offset = new Vector2(distance * MathF.Cos((float)angle.Theta), distance * MathF.Sin((float)angle.Theta));

                validPosition = true;
                foreach (var stationproto in _stationCoords)
                {
                    var minClearance = Math.Max(proto.MinimumClearance, stationproto.minClearance);
                    if (Vector2.Distance(stationproto.position, offset) < minClearance)
                    {
                        validPosition = false;
                        break;
                    }
                }

                if (validPosition)
                    break;
            }

            string overrideName = proto.Name;
            if (i < 26)
                overrideName += $" {(char)('A' + i)}";
            else
                overrideName += $" {i + 1}";

            if (TrySpawnPoiGrid(mapUid, proto, offset, out var uid, overrideName: overrideName) && uid is { Valid: true } station)
            {
                mooringStations.Add(station);
                if (!proto.SpawnOwnMap)
                    AddStationCoordsToSet(offset, proto.MinimumClearance);
            }
        }
    }
}

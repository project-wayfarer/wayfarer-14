using Content.Server.Light.Components;
using Content.Shared.Light.Components;
using Content.Shared.Silicons.Bots;

namespace Content.Server.Silicons.Bots;

/// <summary>
/// System for lightbot functionality.
/// Handles checking if lights need replacement.
/// </summary>
public sealed class LightbotSystem : SharedLightbotSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    /// <summary>
    /// Checks if a light fixture needs a bulb replacement.
    /// Returns true if the light is broken, missing, or burnt out.
    /// </summary>
    public bool NeedsReplacement(EntityUid lightUid, PoweredLightComponent? light = null)
    {
        if (!Resolve(lightUid, ref light, false))
            return false;

        // Check if the light has a bulb
        var bulbUid = light.LightBulbContainer.ContainedEntity;
        
        // Missing bulb needs replacement
        if (bulbUid == null)
            return true;

        // Check if the bulb is broken or burnt
        if (TryComp<LightBulbComponent>(bulbUid, out var bulb))
        {
            return bulb.State != LightBulbState.Normal;
        }

        return false;
    }

    /// <summary>
    /// Gets all light fixtures within range that need replacement.
    /// </summary>
    public IEnumerable<EntityUid> GetBrokenLightsInRange(EntityUid bot, float range)
    {
        var xform = Transform(bot);
        var fixtures = _lookup.GetEntitiesInRange<PoweredLightComponent>(xform.Coordinates, range);

        foreach (var fixture in fixtures)
        {
            if (NeedsReplacement(fixture))
                yield return fixture;
        }
    }
}

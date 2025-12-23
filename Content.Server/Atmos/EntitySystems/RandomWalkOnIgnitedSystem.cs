using Content.Server.Atmos.Components;
using Content.Server.Physics.Components;
using Content.Shared.Atmos;

namespace Content.Server.Atmos.EntitySystems;

/// <summary>
/// Manages entities with RandomWalkOnIgnitedComponent.
/// Adds RandomWalkComponent when ignited, removes it when extinguished.
/// </summary>
public sealed class RandomWalkOnIgnitedSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RandomWalkOnIgnitedComponent, IgnitedEvent>(OnIgnited);
        SubscribeLocalEvent<RandomWalkOnIgnitedComponent, ExtinguishedEvent>(OnExtinguished);
    }

    private void OnIgnited(EntityUid uid, RandomWalkOnIgnitedComponent component, ref IgnitedEvent args)
    {
        // Add RandomWalkComponent if it doesn't exist
        if (!HasComp<RandomWalkComponent>(uid))
        {
            var randomWalk = AddComp<RandomWalkComponent>(uid);
            // Configure the random walk parameters
            // These values match what's in the paperlantern.yml
            randomWalk.AccumulatorRatio = 0.5f;
            randomWalk.MaxSpeed = 1f;
            randomWalk.MinSpeed = 0.25f;
        }
    }

    private void OnExtinguished(EntityUid uid, RandomWalkOnIgnitedComponent component, ref ExtinguishedEvent args)
    {
        // Remove RandomWalkComponent when extinguished
        RemComp<RandomWalkComponent>(uid);
    }
}

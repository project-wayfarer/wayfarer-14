namespace Content.Server.Explosion.EntitySystems;

public sealed partial class ExplosionSystem
{
    // The entity whose explosion is being applied right now.
    public EntityUid? ActiveExplosionCause => _activeExplosion?.Cause;
}

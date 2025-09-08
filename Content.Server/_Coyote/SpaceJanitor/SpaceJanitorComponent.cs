namespace Content.Server._Coyote.SpaceJanitor;

/// <summary>
/// This is a thing that, when added to an entity, will make the SpaceJanitorSystem track it.
/// </summary>
[RegisterComponent]
public sealed partial class SpaceJanitorComponent : Component
{
    /// <summary>
    /// This is the time when the system first found the entity in space.
    /// </summary>
    public TimeSpan FoundInSpaceTime = TimeSpan.Zero;

    /// <summary>
    /// Is this a casing? If so, check if its loaded with something, and
    /// if it ISNT, also treat just being on the floor as also being in space.
    /// Damn things keep piling up, surely cant be good for ram.
    /// </summary>
    [DataField("isCasing")]
    public bool IsCasing = false;
}

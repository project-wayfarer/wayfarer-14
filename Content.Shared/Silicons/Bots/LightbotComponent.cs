using Robust.Shared.GameStates;

namespace Content.Shared.Silicons.Bots;

/// <summary>
/// Component for bots that replace broken or missing light bulbs in fixtures.
/// Similar to cleanbot but for lights.
/// </summary>
[RegisterComponent, NetworkedComponent, Access(typeof(SharedLightbotSystem))]
public sealed partial class LightbotComponent : Component
{
    /// <summary>
    /// The maximum range the lightbot will look for broken lights.
    /// </summary>
    [DataField("range")]
    public float Range = 24f;
}
